using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Golovin_Vibornov_422.services
{
    public static class NavigationService
    {
        private static Frame _mainFrame;

        public static void Initialize(Frame frame)
        {
            _mainFrame = frame;
        }

        public static void NavigateTo(Page page)
        {
            if (_mainFrame != null)
            {
                _mainFrame.Navigate(page);
            }
        }

        public static void GoBack()
        {
            if (_mainFrame != null && _mainFrame.CanGoBack)
            {
                _mainFrame.GoBack();
            }
        }

        public static bool CanGoBack => _mainFrame?.CanGoBack ?? false;
    }
}
