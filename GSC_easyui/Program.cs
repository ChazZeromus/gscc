using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Text;
using System.IO;
using System.Xml.Serialization;

namespace GSC_easyui
{
	class me
	{
		public Action foo;
		public int a = 4;
		public me()
		{
			this.foo = this.bar;
		}

		public void bar()
		{
			a += 1;
		}
	}

	class too
	{
		public Action foo = null;
	}

	static public class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(new Main());
		}
	}
}
