﻿using System;
using System.Windows.Forms;
using MiniSqlQuery.Core;

namespace MiniSqlQuery.PlugIns.ConnectionStringsManager
{
	public partial class DbConnectionsForm : Form
	{
		private DbConnectionDefinitionList _definitionList;

		public DbConnectionsForm()
		{
			InitializeComponent();
			toolStripButtonAdd.Image = ImageResource.database_add;
			toolStripButtonEditConnStr.Image = ImageResource.database_edit;
			toolStripButtonDelete.Image = ImageResource.database_delete;
		}

		private void DbConnectionsForm_Load(object sender, EventArgs e)
		{
			_definitionList = LoadConnectionDefinitions();
			UpdateListView();
		}

		private void UpdateListView()
		{
			if (_definitionList.Definitions != null && _definitionList.Definitions.Length > 0)
			{
				lstConnections.Items.Clear();
				lstConnections.Items.AddRange(_definitionList.Definitions);
				lstConnections.SelectedItem = _definitionList.Definitions[0];
			}
		}

		private void RemoveFromList(DbConnectionDefinition definition)
		{
			if (lstConnections.Items.Contains(definition))
			{
				lstConnections.Items.Remove(definition);
			}
		}

		private void AddToList(DbConnectionDefinition definition)
		{
			if (!lstConnections.Items.Contains(definition))
			{
				lstConnections.Items.Add(definition);
			}
		}

		private void DbConnectionsForm_FormClosing(object sender, FormClosingEventArgs e)
		{
			// todo - confirm changes lost
		}


		private static DbConnectionDefinitionList LoadConnectionDefinitions()
		{
			return DbConnectionDefinitionList.FromXml(Utility.LoadConnections());
		}

		private static void SaveConnectionDefinitions(DbConnectionDefinitionList data)
		{
			ApplicationServices.Instance.Settings.SetConnectionDefinitions(data);
			Utility.SaveConnections(data.ToXml());
		}

		private void toolStripButtonOk_Click(object sender, EventArgs e)
		{
			SaveConnectionDefinitions(_definitionList);
			Close();
		}

		private void toolStripButtonCancel_Click(object sender, EventArgs e)
		{
			Close();
		}

		private void toolStripButtonEditConnStr_Click(object sender, EventArgs e)
		{
			DbConnectionDefinition definition = lstConnections.SelectedItem as DbConnectionDefinition;
			if (definition != null)
			{
				ManageDefinition(definition);
			}
		}

		private void lstConnections_SelectedValueChanged(object sender, EventArgs e)
		{
			DbConnectionDefinition definition = lstConnections.SelectedItem as DbConnectionDefinition;
			UpdateDetailsPanel(definition);
		}

		private void UpdateDetailsPanel(DbConnectionDefinition definition)
		{
			if (definition != null)
			{
				txtName.Text = definition.Name;
				txtProvider.Text = definition.ProviderName;
				txtConn.Text = definition.ConnectionString;
				txtComment.Text = definition.Comment;
			}
			else
			{
				txtName.Clear();
				txtProvider.Clear();
				txtConn.Clear();
				txtComment.Clear();
			}
		}

		private void lstConnections_DoubleClick(object sender, EventArgs e)
		{
			DbConnectionDefinition definition = lstConnections.SelectedItem as DbConnectionDefinition;
			if (definition != null)
			{
				ManageDefinition(definition);
			}
		}

		private void toolStripButtonAdd_Click(object sender, EventArgs e)
		{
			ManageDefinition(null);
		}

		private void toolStripButtonDelete_Click(object sender, EventArgs e)
		{
			DbConnectionDefinition definition = lstConnections.SelectedItem as DbConnectionDefinition;
			if (definition != null)
			{
				_definitionList.RemoveDefinition(definition);
				RemoveFromList(definition);
			}
		}

		private void ManageDefinition(DbConnectionDefinition definition)
		{
			ConnectionStringBuilderForm frm;

			if (definition == null)
			{
				frm = new ConnectionStringBuilderForm(); // new blank form
			}
			else
			{
				frm = new ConnectionStringBuilderForm(definition);
			}

			frm.ShowDialog(this);

			if (frm.DialogResult == DialogResult.OK)
			{
				if (definition == null)
				{
					_definitionList.AddDefinition(frm.ConnectionDefinition);
					AddToList(frm.ConnectionDefinition);
					lstConnections.SelectedItem = frm.ConnectionDefinition;
				}
			}
		}
	}
}