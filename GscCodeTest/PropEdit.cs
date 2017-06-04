using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using GameScriptCompiler;

namespace GscCodeTest
{
	public partial class OptionEdit : Form
	{
		Object Options;
		public OptionEdit(Object options)
		{
			InitializeComponent();
			propertyGrid1.SelectedObject = this.Options = options;
			if (options.GetType() != typeof(CompilerOptions))
			{
				this.Controls.Remove(buttonSave);
				this.propertyGrid1.Dock = DockStyle.Fill;
			}
		}

		private void PropEdit_FormClosing(object sender, FormClosingEventArgs e)
		{
			e.Cancel = true;
			this.Hide();
		}

		private void buttonSave_Click(object sender, EventArgs e)
		{
			var opt = Options as CompilerOptions;
			try
			{
				using (var s = File.Open(MainForm.SettingsFile, FileMode.Truncate))
					opt.SaveOptions(s);
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.ToString());
			}
		}
	}
}
