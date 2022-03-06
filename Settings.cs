using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace ModelBrowser3D.Presentation
{
    internal class Settings
    {
        public List<string> SelectedFolders { get; set; } = new List<string>();
        public string LastPath { get; set; }
        public string SelectedColor { get; set; }
        public double SelectedAltitude { get; set; }
        public double SelectedAzimuth { get;  set; }
    }
}
