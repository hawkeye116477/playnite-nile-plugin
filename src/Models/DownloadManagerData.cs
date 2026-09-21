using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommonPlugin.Enums;
using Playnite.SDK.Data;

namespace NileLibraryNS.Models
{
    public class DownloadManagerData
    {
        public ObservableCollection<Download> downloads { get; set; }

        public class Download : ObservableObject
        {
            public string gameID { get; set; }
            public string name { get; set; }
            public string fullInstallPath { get; set; }

            private double _downloadSizeNumber;

            [DontSerialize]
            public double downloadSizeNumber
            {
                get => _downloadSizeNumber;
                set => SetValue(ref _downloadSizeNumber, value);
            }

            [DontSerialize]
            public long addedTime { get; set; }

            private long _completedTime;

            [DontSerialize]
            public long completedTime
            {
                get => _completedTime;
                set => SetValue(ref _completedTime, value);
            }

            private DownloadStatus _status;

            [DontSerialize]
            public DownloadStatus status
            {
                get => _status;
                set => SetValue(ref _status, value);
            }

            private double _progress;

            [DontSerialize]
            public double progress
            {
                get => _progress;
                set => SetValue(ref _progress, value);
            }

            private double _downloadedNumber;

            [DontSerialize]
            public double downloadedNumber
            {
                get => _downloadedNumber;
                set => SetValue(ref _downloadedNumber, value);
            }

            public DownloadProperties downloadProperties { get; set; } = new DownloadProperties();
        }
    }

    public class DownloadProperties : ObservableObject
    {
        public string installPath { get; set; } = "";
        public DownloadAction downloadAction { get; set; }
        public int maxWorkers { get; set; }
    }
}