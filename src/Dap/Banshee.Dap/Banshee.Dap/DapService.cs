//
// DapService.cs
//
// Authors:
//   Gabriel Burt <gburt@novell.com>
//   Aaron Bockover <abockover@novell.com>
//   Ruben Vermeersch <ruben@savanne.be>
//
// Copyright (C) 2007-2008 Novell, Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using Mono.Unix;
using Mono.Addins;

using Hyena;
using Banshee.Base;
using Banshee.ServiceStack;
using Banshee.Sources;
using Banshee.Collection;
using Banshee.Collection.Database;
using Banshee.Hardware;

namespace Banshee.Dap
{
    public class DapService : IExtensionService, IDelayedInitializeService, IDisposable
    {
        private Dictionary<string, DapSource> sources;
        private List<TypeExtensionNode> supported_dap_types = new List<TypeExtensionNode> ();
        private bool initialized;
        private object sync = new object ();

        public void Initialize ()
        {
        }

        public void DelayedInitialize ()
        {
            lock (sync) {
                sources = new Dictionary<string, DapSource> ();
                
                AddinManager.AddExtensionNodeHandler ("/Banshee/Dap/DeviceClass", OnExtensionChanged);
                
                ServiceManager.HardwareManager.DeviceAdded += OnHardwareDeviceAdded;
                ServiceManager.HardwareManager.DeviceRemoved += OnHardwareDeviceRemoved;
                ServiceManager.SourceManager.SourceRemoved += OnSourceRemoved;
                initialized = true;
            }
        }

        private void OnExtensionChanged (object o, ExtensionNodeEventArgs args) 
        {
            lock (sync) {
                TypeExtensionNode node = (TypeExtensionNode)args.ExtensionNode;
                
                if (args.Change == ExtensionChange.Add) {
                    Log.DebugFormat ("Dap support extension loaded: {0}", node.Addin.Id);
                    supported_dap_types.Add (node);
    
                    // See if any existing devices are handled by this new DAP support
                    foreach (IDevice device in ServiceManager.HardwareManager.GetAllDevices ()) {
                        MapDevice (device);
                    }
                } else if (args.Change == ExtensionChange.Remove) {
                    supported_dap_types.Remove (node);
                    
                    Queue<DapSource> to_remove = new Queue<DapSource> ();
                    foreach (DapSource source in sources.Values) {
                        if (source.AddinId == node.Addin.Id) {
                            to_remove.Enqueue (source);
                        }
                    }
                    
                    while (to_remove.Count > 0) {
                        UnmapDevice (to_remove.Dequeue ().Device.Uuid);
                    }
                }
            }
        }

        public void Dispose ()
        {
            lock (sync) {
                if (!initialized)
                    return;

                AddinManager.RemoveExtensionNodeHandler ("/Banshee/Dap/DeviceClass", OnExtensionChanged);
                
                ServiceManager.HardwareManager.DeviceAdded -= OnHardwareDeviceAdded;
                ServiceManager.HardwareManager.DeviceRemoved -= OnHardwareDeviceRemoved;
                ServiceManager.SourceManager.SourceRemoved -= OnSourceRemoved;
                
                List<DapSource> dap_sources = new List<DapSource> (sources.Values);
                foreach (DapSource source in dap_sources) {
                    UnmapDevice (source.Device.Uuid);
                }
                
                sources.Clear ();
                sources = null;
            }
        }
        
        private DapSource FindDeviceSource (IDevice device)
        {
            foreach (TypeExtensionNode node in supported_dap_types) {
                try {
                    DapSource source = (DapSource)node.CreateInstance ();
                    source.DeviceInitialize (device);
                    source.LoadDeviceContents ();
                    source.AddinId = node.Addin.Id;
                    return source;
                } catch (InvalidDeviceException) {
                } catch (InvalidCastException e) {
                    Log.Exception ("Extension is not a DapSource as required", e);
                } catch (Exception e) {
                    Log.Exception (e);
                }
            }
            
            return null;
        }
        
        private void MapDevice (IDevice device)
        {
            Banshee.Kernel.Scheduler.Schedule (new Banshee.Kernel.DelegateJob (delegate {
                lock (sync) {
                    try {
                        if (sources.ContainsKey (device.Uuid)) {
                            return;
                        }
                        
                        if (device is ICdromDevice || device is IDiscVolume) {
                            return;
                        }
                        
                        if (device is IVolume && (device as IVolume).ShouldIgnore) {
                            return;
                        }
                        
                        if (device.MediaCapabilities == null && !(device is IBlockDevice) && !(device is IVolume)) {
                            return;
                        }
                        
                        DapSource source = FindDeviceSource (device);
                        if (source != null) {
                            Log.DebugFormat ("Found DAP support ({0}) for device {1}", source.GetType ().FullName, source.Name);
                            sources.Add (device.Uuid, source);
                            ServiceManager.SourceManager.AddSource (source);
                            source.NotifyUser ();
                        }
                    } catch (Exception e) {
                        Log.Exception (e);
                    }
                }
            }));
        }
        
        internal void UnmapDevice (string uuid)
        {
            lock (sync) {
                if (sources.ContainsKey (uuid)) {
                    Log.DebugFormat ("Unmapping DAP source ({0})", uuid);
                    DapSource source = sources[uuid];
                    source.Dispose ();
                    sources.Remove (uuid);
                    ServiceManager.SourceManager.RemoveSource (source);
                }
            }
        }

        private void OnSourceRemoved (SourceEventArgs args)
        {
            DapSource dap_source = args.Source as DapSource;
            if (dap_source != null) {
                lock (sync) {
                    UnmapDevice (dap_source.Device.Uuid);
                }
            }
        }
        
        private void OnHardwareDeviceAdded (object o, DeviceAddedArgs args)
        {
            lock (sync) {
                MapDevice (args.Device);
            }
        }
        
        private void OnHardwareDeviceRemoved (object o, DeviceRemovedArgs args)
        {
            lock (sync) {
                UnmapDevice (args.DeviceUuid);
            }
        }
        
        string IService.ServiceName {
            get { return "DapService"; }
        }
    }
}
