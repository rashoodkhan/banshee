//
// Client.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2008 Novell, Inc.
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

using Hyena;
using Banshee.Base;
using Banshee.ServiceStack;
using Banshee.MediaEngine;
using Banshee.PlaybackController;

namespace Halie
{
    public static class Client
    {
        public static void Main ()
        {
            if (!DBusConnection.ConnectTried) {
                DBusConnection.Connect ();
            }
            
            if (!DBusConnection.Enabled) {
                Error ("All commands ignored, DBus support is disabled");
                return;
            } else if (!DBusConnection.InstanceAlreadyRunning) {
                Error ("Banshee does not seem to be running");
                return;
            }
            
            HandlePlayerCommands ();
        }
        
        private static void HandlePlayerCommands ()
        {
            IPlayerEngineService player = DBusServiceManager.FindInstance<IPlayerEngineService> ("/PlayerEngine");
            IPlaybackControllerService controller = DBusServiceManager.FindInstance<IPlaybackControllerService> ("/PlaybackController");
            IDictionary<string, object> track = null;
            
            foreach (KeyValuePair<string, string> arg in ApplicationContext.CommandLine.Arguments) {
                switch (arg.Key) {
                    // For the player engine
                    case "play":           player.Play ();          break;
                    case "pause":          player.Pause ();         break;
                    case "stop":           player.Close ();         break;
                    case "toggle-playing": player.TogglePlaying (); break;
                    
                    // For the playback controller
                    case "first":    controller.First ();                                    break;
                    case "next":     controller.Next (ParseBool (arg.Value, "restart"));     break;
                    case "previous": controller.Previous (ParseBool (arg.Value, "restart")); break;
                    case "stop-when-finished": 
                        controller.StopWhenFinished = !ParseBool (arg.Value);
                        break;
                    default:
                        if (arg.Key.StartsWith ("query-")) {
                            if (track == null) {
                                track = player.CurrentTrack;
                            }
                            HandleQuery (player, track, arg.Key.Substring (6));
                        }
                        break;
                }
            }
        }
        
        private static void HandleQuery (IPlayerEngineService player, IDictionary<string, object> track, string query)
        {
            // Translate legacy query arguments into new ones
            switch (query) {
                case "title":    query = "name";   break;
                case "duration": query = "length"; break;
                case "uri":      query = "URI";    break;
            }
            
            switch (query) {
                case "all":
                    foreach (KeyValuePair<string, object> field in track) {
                        DisplayTrackField (field.Key, field.Value);
                    }
                    
                    HandleQuery (player, track, "position");
                    HandleQuery (player, track, "volume");
                    HandleQuery (player, track, "current-state");
                    HandleQuery (player, track, "last-state");
                    HandleQuery (player, track, "can-pause");
                    HandleQuery (player, track, "can-seek");
                    break; 
                case "position": 
                    DisplayTrackField ("position", TimeSpan.FromMilliseconds (player.Position).TotalSeconds); 
                    break;
                case "volume":
                    DisplayTrackField ("volume", player.Volume);
                    break;
                case "current-state":
                    DisplayTrackField ("current-state", player.CurrentState);
                    break;
                case "last-state":
                    DisplayTrackField ("last-state", player.LastState);
                    break;
                case "can-pause":
                    DisplayTrackField ("can-pause", player.CanPause);
                    break;
                case "can-seek":
                    DisplayTrackField ("can-seek", player.CanSeek);
                    break;
                default:
                    if (track.ContainsKey (query)) {
                        DisplayTrackField (query, track[query]);
                    } else {
                        Error ("'{0}' field unknown", query);
                    }
                    break;
            }
        }
        
        private static void DisplayTrackField (string field, object value)
        {
            string result = null;
            if (value is bool) {
                result = (bool)value ? "true" : "false";
            } else {
                result = value.ToString ();
            }
            
            Console.WriteLine ("{0}: {1}", field, result);
        }
        
        private static bool ParseBool (string value)
        {
            return ParseBool (value, "true", "yes");
        }
        
        private static bool ParseBool (string value, params string [] trueValues)
        {
            if (String.IsNullOrEmpty (value)) {
                return false;
            }
            
            value = value.ToLower ();
            
            foreach (string trueValue in trueValues) {
                if (value == trueValue) {
                    return true;
                }
            }
            
            return false;
        }
        
        private static void Error (string error, params object [] args)
        {
            Console.WriteLine ("Error: {0}", String.Format (error, args));
        }
    }
}

