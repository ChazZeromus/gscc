using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using GameScriptCompiler.ContextAnalyzation;

namespace LogReader
{
	public partial class progress : Form
	{
		MessageObject[] msgs;
		Form1 mainform;
		public Boolean state = false;
		public int done = 0;
		public HashSet<String> types = new HashSet<string>();
		public Boolean doEach = false;
		public enum filterType
		{
			each,
			filter
		}
		public class filterOption
		{
			public String text;
			public filterType type;
			public filterOption(String text, filterType type)
			{
				this.type = type;
				this.text = text;
			}

			public override string ToString()
			{
				return text;
			}
		}
		public progress(Form1 mainform)
		{
			this.mainform = mainform;
			InitializeComponent();
			this.comboBox1.Items.AddRange(new Object[] { new filterOption("One of each", filterType.each),
				new filterOption("Custom", filterType.filter) });
			this.comboBox1.SelectedIndex = 0;
		}

		public void start(MessageObject[] msgs)
		{
			this.msgs = msgs;
			this.state = false;
			this.types.Clear();
			update();
			this.Show();
		}

		public void update()
		{
			this.comboBox1.Enabled = !state;
			if (!state)
			{
				this.label1.Text = "Click start to being adding";
				this.button1.Text = "Start";
			}
			else
			{
				this.label1.Text = "Adding...";
				this.button1.Text = "Stop";
			}
		}

		void dispatchwork()
		{
			this.types.Clear();
			this.mainform.clearLog();
			this.doEach = this.comboBox1.SelectedItem != null && (this.comboBox1.SelectedItem as filterOption).type == filterType.each;
			mainform.BeginInvoke(new Action(this.workthread));
		}


		public void workthread()
		{
			int i = 1;
			foreach (var mo in this.msgs)
			{
				if (!this.state)
					break;
				if (!this.doEach)
					this.mainform.Add(mo, i++);
				else if (!this.types.Contains(mo.Msg))
				{
					this.mainform.Add(mo, i++);
					this.types.Add(mo.Msg);
				}

				this.Invoke(new Action(() =>
				{
					this.label1.Text = String.Format("{0}/{1}", i, this.msgs.Length);
				}));
			}
			this.Invoke(new Action(() =>
			{
				this.state = false;
				this.update();
				this.Hide();
				this.mainform.DoneAdding();
			}));
		}

		private void button1_Click(object sender, EventArgs e)
		{
			if (!this.state)
			{
				this.state = true;
				this.update();
				this.dispatchwork();
			}
			else
			{
				this.state = false;
				this.update();
			}
		}
	}
}
