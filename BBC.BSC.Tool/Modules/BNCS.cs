using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BBC.BSC.Tool.Modules
{
    public class BNCS
    {
        public static PackIconKind GetPackIconKind(string ext)
        {
            PackIconKind packIconKind;
            switch (ext)
            {
                case "vnc":
                    packIconKind = PackIconKind.Computer;
                    break;
                case "url":
                    packIconKind = PackIconKind.Web;
                    break;
                case "lnk":
                    packIconKind = PackIconKind.FolderNetwork;
                    break;
                case "rdp":
                    packIconKind = PackIconKind.Server;
                    break;
                default:
                    packIconKind = PackIconKind.HelpBox;
                    break;
            }
            return packIconKind;
        }
    }
}
