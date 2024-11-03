using CommonPlugin.Enums;
using Playnite.SDK;
using System;
using System.Globalization;
using System.Windows.Data;

namespace NileLibraryNS.Converters
{
    public class DownloadStatusEnumToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch (value)
            {
                case DownloadStatus.Queued:
                    value = ResourceProvider.GetString(LOC.NileDownloadQueued);
                    break;
                case DownloadStatus.Running:
                    value = ResourceProvider.GetString(LOC.NileDownloadRunning);
                    break;
                case DownloadStatus.Canceled:
                    value = ResourceProvider.GetString(LOC.NileDownloadCanceled);
                    break;
                case DownloadStatus.Paused:
                    value = ResourceProvider.GetString(LOC.NileDownloadPaused);
                    break;
                case DownloadStatus.Completed:
                    value = ResourceProvider.GetString(LOC.NileDownloadCompleted);
                    break;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
