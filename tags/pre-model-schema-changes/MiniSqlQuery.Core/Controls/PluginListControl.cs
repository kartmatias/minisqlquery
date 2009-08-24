﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;

namespace MiniSqlQuery.Core.Controls
{
	/// <summary>
	/// A simple control to display plugin details.
	/// </summary>
	public partial class PluginListControl : UserControl
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="PluginListControl"/> class.
		/// </summary>
		public PluginListControl()
		{
			InitializeComponent();
		}

		/// <summary>
		/// Sets the data source to the <paramref name="plugins"/>.
		/// </summary>
		/// <param name="plugins">The plugins to display.</param>
		public void SetDataSource(IPlugIn[] plugins)
		{
			foreach (IPlugIn plugin in plugins)
			{
				ListViewItem item = new ListViewItem(new string[] {
					plugin.PluginName, plugin.PluginDescription, plugin.GetType().Assembly.FullName});
				listView1.Items.Add(item);
			}
		}
	}
}