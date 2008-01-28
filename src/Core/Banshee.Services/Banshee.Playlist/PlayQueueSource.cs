//
// PlayQueueSource.cs
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
using System.Data;
using Mono.Unix;

using Hyena.Data.Sqlite;

using Banshee.ServiceStack;
using Banshee.Database;
using Banshee.Collection.Database;

namespace Banshee.Playlist
{
    public class PlayQueueSource : PlaylistSource
    {
        private static string special_playlist_name = typeof (PlayQueueSource).ToString ();
        
        private static PlayQueueSource instance;
        public static PlayQueueSource Instance {
            get { return instance; }
        }
    
        public PlayQueueSource () : base (Catalog.GetString ("Play Queue"), null)
        {
            BindToDatabase ();
            
            Order = 0;
            Properties.SetString ("IconName", "audio-x-generic");
            
            ((TrackListDatabaseModel)TrackModel).ForcedSortQuery = "CorePlaylistEntries.EntryID DESC";
            
            if (instance == null) {
                instance = this;
            }
        }
        
        private void BindToDatabase ()
        {
            object result = ServiceManager.DbConnection.ExecuteScalar (new HyenaSqliteCommand (@"
                SELECT PlaylistID FROM CorePlaylists 
                    WHERE Special = 1 AND Name = ?
                    LIMIT 1", special_playlist_name));
            
            if (result != null) {
                DbId = Convert.ToInt32 (result);
            } else {
                CreateDatabaseEntry (ServiceManager.DbConnection.Connection);
                DbId = ServiceManager.DbConnection.LastInsertRowId;
            }
        }
        
        public override bool CanRename {
            get { return false; }
        }
        
        public override bool ShowBrowser {
            get { return false; }
        }
    
        // We have to use System.Data level API here since this is called inside
        // of BansheeDbFormatMigrator and thus ServiceManager.DbConnection is not
        // yet available for use.
        internal static void CreateDatabaseEntry (IDbConnection connection)
        {
            IDbCommand command = connection.CreateCommand ();
            
            IDbDataParameter parameter = command.CreateParameter ();
            parameter.ParameterName = "playlist_name";
            parameter.Value = special_playlist_name;
            command.Parameters.Add (parameter);
            
            command.CommandText = "INSERT INTO CorePlaylists VALUES (0, :playlist_name, -1, 0, 1)";
            command.ExecuteNonQuery ();
        }
    }
}
