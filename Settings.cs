using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace _3DModelBrowser
{
    internal class Settings
    {
        public List<string> SelectedFolders { get; set; } = new List<string>();
        public string LastPath { get; set; }
    }
}
