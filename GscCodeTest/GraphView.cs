using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using GameScriptCompiler.CodeDOM;

namespace GscCodeTest
{
	public partial class GraphView : Form
	{
		MainForm MF;
		public GraphView(MainForm mf)
		{
			MF = mf;
			InitializeComponent();
		}

		static TreeNode CreateNode(Expression expr)
		{
			var tn = new TreeNode();
			tn.Text = expr == null ? "<null>" : expr.ToString();
			tn.Tag = expr;
			return tn;
		}

		public class NodeProg
		{
			public IEnumerator<Expression> It;
			public TreeNodeCollection Parent;
		}

		public void SetUpNodes(Expressable block)
		{
			treeView1.Nodes.Clear();
			if (block == null)
				return;
			Text = block is CodeDOMModule ? "Module Code Graph" : (block is FunctionDefinition ? "Statement Code Graph" : "Expression Code Graph");
			var nodestack = new Stack<NodeProg>();
			NodeProg current;
			if (block is Expression)
				nodestack.Push(new NodeProg
				{
					It = (new Expression[] { block as Expression }).AsEnumerable().GetEnumerator(),
					Parent = treeView1.Nodes
				});
			else
				if (block is CodeDOMModule || block is FunctionDefinition)
				{
					IEnumerable<FunctionDefinition> target =
						block is CodeDOMModule ? (IEnumerable<FunctionDefinition>)(block as CodeDOMModule).GetDefinitions() :
						new FunctionDefinition[] { block as FunctionDefinition };
					foreach (var fd in target)
					{
						var tn = new TreeNode()
						{
							Text = fd.Name,
							Tag = fd
						};
						treeView1.Nodes.Add(tn);
						nodestack.Push(new NodeProg
						{
							It = fd.RootStatement.GetChildren().GetEnumerator(),
							Parent = tn.Nodes
						});
					}
					
					List<TreeNode> readytosort = new List<TreeNode>();
					foreach (TreeNode tn in treeView1.Nodes)
						readytosort.Add(tn);
					readytosort.Sort(new Comparison<TreeNode>((t1, t2) => t1.Text.CompareTo(t2.Text)));
					treeView1.Nodes.Clear();
					treeView1.Nodes.AddRange(readytosort.ToArray());
				}
			while (nodestack.Count > 0)
			{
				current = nodestack.Pop();
				while (current.It.MoveNext())
				{
					var c = current.It.Current;
					if (c == null)
						continue;
					var tn = CreateNode(c);
					current.Parent.Add(tn);
					if (c == null || c.GetChildren() == null)
						continue;
					nodestack.Push(current);
					current = new NodeProg() { Parent = tn.Nodes, It = c.GetChildren().GetEnumerator()};
				}
			}
		}

		private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
		{
			richTextBoxOutput.Text = e.Node.Tag == null ? "" : (e.Node.Tag as Expressable).Express(0);
		}

		private void GraphView_FormClosing(object sender, FormClosingEventArgs e)
		{
			e.Cancel = true;
			this.Hide();
		}

		private void inlineEvaluateToolStripMenuItem_Click(object sender, EventArgs e)
		{
			MF.Eval(treeView1.SelectedNode.Tag as Expression);
		}

		private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
		{
			inlineEvaluateToolStripMenuItem.Enabled = treeView1.SelectedNode != null && treeView1.SelectedNode.Tag != null
				&& treeView1.SelectedNode.Tag is Expression;
		}

		private void treeView1_DoubleClick(object sender, EventArgs e)
		{
			if (treeView1.SelectedNode != null)
				if (treeView1.SelectedNode.Tag is Expression)
					MF.GoToLoc((treeView1.SelectedNode.Tag as Expression).Location);
				else
					if (treeView1.SelectedNode.Tag is FunctionDefinition)
						MF.GoToLoc((treeView1.SelectedNode.Tag as FunctionDefinition).Location);
		}
	}
}
