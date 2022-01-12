using Microsoft.VisualStudio.TestTools.UnitTesting;
using BBC.BSC.Tool.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MaterialDesignThemes.Wpf;

namespace BBC.BSC.Tool.Modules.Tests
{
    [TestClass()]
    public class BncsTests
    {
        [TestMethod()]
        public void GetPackIconKindTest()
        {
            
            Assert.AreEqual(Bncs.GetPackIconKind("vnc"), PackIconKind.Computer);

        }
    }
}