using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace GscCodeTest
{
	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		enum et
		{
			a, b
		}
		[STAThread]
		static void Main()
		{
			string a = "blah";
			int p = 0;

			a += p;

			String s = Enum.GetNames(typeof(et)).FirstOrDefault(e => e == "a");
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(new MainForm());
		}
	}
}
