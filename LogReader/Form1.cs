using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Xml.Serialization;
using System.Diagnostics;
using GameScriptCompiler.ContextAnalyzation;

namespace LogReader
{
	public partial class Form1 : Form
	{
		public progress prog;
		public static readonly String settingsfile = "logreadersettings.xml";
		public settingsobj settings = new settingsobj();

		public class settingsobj
		{
			public List<String> recent = new List<string>();
			public String program = "notepad.exe", args = "{0}";
			public void save()
			{
				var xs = new XmlSerializer(this.GetType());
				using (var stream = File.OpenWrite(Form1.settingsfile))
					xs.Serialize(stream, this);
			}
			public void load()
			{
				if (File.Exists(Form1.settingsfile))
				{
					var xs = new XmlSerializer(this.GetType());
					using (var stream = File.OpenRead(Form1.settingsfile))
					{
						try
						{
							var x = xs.Deserialize(stream) as settingsobj;
							this.recent = x.recent;
							this.program = x.program;
							this.args = x.args;
						}
						catch (Exception e)
						{
							MessageBox.Show(e.ToString());
							return;
						}
					}
				}
			}
		}

		public Form1()
		{
			InitializeComponent();
			this.logview.ListViewItemSorter = new Sorter();
			this.prog = new progress(this);
			settings.load();
			settings.save();
			updateform();
		}

		public void updateform()
		{
			this.textBox1.Text = settings.args;
			this.textBox2.Text = settings.program;
		}


		public void ReadLog(Stream stream, ref MessageObject[] output)
		{
			var xs = new XmlSerializer(typeof(MessageObject[]));
			output = xs.Deserialize(stream) as MessageObject[];
		}

		public void clearLog()
		{
			this.logview.Items.Clear();
		}

		private void openToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (this.prog.state)
				return;
			var ofd = new OpenFileDialog()
			{
				CheckFileExists = true,
				Title = "Select Log file",
				Multiselect = false,
				DefaultExt = "*.*"
			};
			if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
				return;
			var stream = ofd.OpenFile();
			if (stream == null)
				return;
			settings.recent.Add(ofd.FileName);
			if (settings.recent.Count > 15)
				settings.recent.RemoveAt(0);
			this.settings.save();
			try
			{
				var mo = new MessageObject[] {};
				ReadLog(stream, ref mo);
				stream.Close();
				if (mo == null)
					return;

				logview.Items.Clear();

				this.prog.start(mo);
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.ToString());
			}
		}

		public void DoneAdding()
		{
			logview.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
		}

		public void Add(MessageObject mo, int num)
		{
			var lvi = new ListViewItem(num.ToString()) { Tag = mo };
			lvi.SubItems.AddRange(new[] {
				mo.MsgType.ToString(),
				mo.Msg,
				mo.Module,
				mo.GetFirstLocation().ToString(),
				mo.Target
			});
			logview.Items.Add(lvi);
		}

		private void logview_ColumnClick(object sender, ColumnClickEventArgs e)
		{
			ColumnHeader header = this.logview.Columns[e.Column];
			var aes = this.logview.ListViewItemSorter as Sorter;
			aes.targetColumn = e.Column;
			if (header.Tag == null)
				header.Tag = -1;
			aes.direction = (int)header.Tag;
			this.logview.Sort();
			header.Tag = (int)header.Tag * -1;
		}

		public class Sorter : System.Collections.IComparer
		{
			public int targetColumn = 0, direction = 1;
			public int Compare(Object x, Object y)
			{
				var lvi_x = x as ListViewItem;
				var lvi_y = y as ListViewItem;
				return lvi_x.SubItems[this.targetColumn].Text.CompareTo(lvi_y.SubItems[this.targetColumn].Text) * direction;
			}
		}

		private void textBox1_MouseLeave(object sender, EventArgs e)
		{
			if (textBox1.Modified)
			{
				settings.save();
				textBox1.Modified = false;
			}
		}

		private void textBox1_TextChanged(object sender, EventArgs e)
		{
			settings.args = textBox1.Text;
		}

		private void logview_DoubleClick(object sender, EventArgs e)
		{
			foreach (MessageObject item in logview.SelectedItems.Cast<ListViewItem>().Select(s => s.Tag).Cast<MessageObject>())
			{
				var loc = item.GetFirstLocation();
				ProcessStartInfo psi = new ProcessStartInfo();
				psi.FileName = settings.program;
				psi.Arguments = String.Format(settings.args, item.Module, loc.Value.Line, loc.Value.Column);
				psi.UseShellExecute = true;
				try
				{
					Process.Start(psi);
				}
				catch (Exception ex)
				{
					MessageBox.Show(ex.ToString());
				}
			}
		}



		private void recentToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
		{
			recentToolStripMenuItem.DropDownItems.Clear();
			foreach (var file in settings.recent)
			{
				ToolStripMenuItem item = new ToolStripMenuItem();
				item.Text = (new FileInfo(file)).Name;
				item.Tag = file;
				item.Click += item_Click;
				recentToolStripMenuItem.DropDownItems.Add(item);
			}
		}

		void item_Click(object sender, EventArgs e)
		{
			var item = sender as ToolStripMenuItem;
			using (var stream = File.Open(item.Tag as String, FileMode.Open))
				try
				{
					var mo = new MessageObject[] { };
					ReadLog(stream, ref mo);
					stream.Close();
					if (mo == null)
						return;
					logview.Items.Clear();
					this.prog.start(mo);
				}
				catch (Exception ex)
				{
					MessageBox.Show(ex.ToString());
				}
		}

		private void textBox2_MouseLeave(object sender, EventArgs e)
		{
			if (textBox2.Modified)
			{
				settings.save();
				textBox2.Modified = false;
			}
		}

		private void textBox2_TextChanged(object sender, EventArgs e)
		{
			settings.program = textBox2.Text;
		}
	}
}
