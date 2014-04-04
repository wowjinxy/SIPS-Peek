﻿using Be.Windows.Forms;
using BrightIdeasSoftware;
using IpsLibNet;
using IpsPeek.IpsLibNet.Patching;
using IpsPeek.Options;
using IpsPeek.Reporting;
using IpsPeek.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace IpsPeek
{
    public partial class FormMain : Form
    {
        private long _fileSize = 0;
        private int _patchCount = 0;
        private string _fileName;
        private int _modified = 0;
        private HighlightTextRenderer _highlighter = new HighlightTextRenderer();
        private readonly string optionsPath = Path.Combine(Application.StartupPath, "settings");
        #region "Helpers"
        private void CloseFile()
        {
            fastObjectListViewRows.ClearObjects();
            hexBoxData.ByteProvider = null;
            this.Text = Application.ProductName;

            this.closeToolStripMenuItem.Enabled = false;
            this.closeToolStripButton.Enabled = false;

            exportToolStripButton.Enabled = false;
            exportToolStripMenuItem.Enabled = false;

            goToRowToolStripMenuItem.Enabled = false;
            toolStripButtonGoToRow.Enabled = false;


            toolStripStatusLabelRows.Text = string.Format(Strings.Row, 0, 0, 0);
            ToolStripStatusLabelPatchCount.Text = string.Format(Strings.Patches, 0);
            toolStripStatusLabelFileSize.Text = string.Empty;
            toolStripStatusLabelModified.Text = string.Format(Strings.Modified, 0);
            this.olvColumnNumber.Tag = 0;
        }
        private void SetStrings()
        {
            olvColumnEnd.Text = Strings.End;
            olvColumnIpsEnd.Text = Strings.IpsEnd;
            olvColumnIpsOffset.Text = Strings.IpsOffset;
            olvColumnIpsSize.Text = Strings.IpsSize;
            olvColumnIpsSizeHex.Text = Strings.IpsSizeHex;
            olvColumnOffset.Text = Strings.Offset;
            olvColumnSize.Text = Strings.Size;
            olvColumnSizeHex.Text = Strings.SizeHex;
            olvColumnType.Text = Strings.Type;

            fileToolStripMenuItem.Text = Strings.File;
            openPatchToolStripMenuItem.Text = Strings.Open;
            closeToolStripMenuItem.Text = Strings.Close;
            exportToolStripMenuItem.Text = Strings.Export;
            exitToolStripMenuItem.Text = Strings.Exit;

            viewToolStripMenuItem.Text = Strings.View;
            toolbarToolStripMenuItem.Text = Strings.Toolbar;
            dataViewToolStripMenuItem.Text = Strings.DataView;
            stringViewToolStripMenuItem.Text = Strings.StringView;

            helpContentsToolStripMenuItem.Text = Strings.Help;
            helpContentsToolStripMenuItem.Text = Strings.HelpContents;
            iPSPeekHomeToolStripMenuItem.Text = Strings.ApplicationHome;
            officialForumToolStripMenuItem.Text = Strings.OfficialForum;
            aboutIPSPeekToolStripMenuItem.Text = Strings.About;

            openPatchToolStripButton.Text = Strings.Open;
            closeToolStripButton.Text = Strings.Close;
            exportToolStripButton.Text = Strings.Export;

        }
        private void OpenFile()
        {

            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = Strings.FilterIpsFiles;

                if (dialog.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
                {
                    _fileName = Path.GetFileName(dialog.FileName);
                    LoadFile(dialog.FileName);
                    filterToolStripTextBox.Clear();
                }
            }
        }
        private void OpenPage(string url)
        {
            Process.Start(url);
        }

        private string GetDisplayName(Type element)
        {
            if (element == typeof(IpsEndOfFileValueElement))
            {
                return "EOF";
            }
            else if (element == typeof(IpsIdValueElement))
            {
                return "ID";
            }
            else if (element == typeof(IpsPatchElement))
            {
                return "PAT";
            }
            else if (element == typeof(IpsResizeValueElement))
            {
                return "CHS";
            }

            else if (element == typeof(IpsRlePatchElement))
            {
                return "RLE";
            }
            else
            {
                return string.Empty;
            }

        }

        private void ExportFile()
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = Strings.FilterTextFiles;
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {

                    using (ITableWriter writer = new TableStreamWriter(dialog.OpenFile()))
                    {
                        try
                        {
                            var sr = ((StreamWriter)writer);
                            sr.WriteLine(Strings.ApplicationInformation, Application.ProductName, Application.ProductVersion.ToString());
                            sr.WriteLine();
                            sr.WriteLine(Strings.FileInformation, _fileName);
                            sr.WriteLine();

                            List<Cell> row = new List<Cell>();
                            List<OLVColumn> columns = fastObjectListViewRows.AllColumns.Where((c) => c.IsVisible).OrderBy((c) => c.DisplayIndex).ToList();
                            foreach (OLVColumn column in columns)
                            {
                                var cell = new Cell(column.Text, (int)column.Tag);
                                cell.Padding = 1;

                                row.Add(cell);
                            }
                            writer.WriteRow(row.ToArray());
                            row.Clear();

                            foreach (var patch in fastObjectListViewRows.Objects)
                            {
                                foreach (OLVColumn column in columns)
                                {

                                    string text = column.AspectGetter(patch).ToString();
                                    if (string.IsNullOrEmpty(text)) text = text.PadLeft((int)column.Tag, '-');

                                    var cell = new Cell(text, Math.Max(column.Text.Length, (int)column.Tag));
                                    cell.Padding = 1;
                                    row.Add(cell);
                                }
                                writer.WriteRow(row.ToArray());
                                row.Clear();

                            }
                            sr.WriteLine("Rows: {0:X} ({0}), Patches: {1:X} ({1}), Modified: {2:X} ({2})", fastObjectListViewRows.GetItemCount(), _patchCount, _modified);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                        }
                    }
                }
            }
        }

        private void LoadFile(string file)
        {
            try
            {
                var scanner = new IpsScanner();
                List<IpsElement> patches = scanner.Scan(file);
                _patchCount = patches.Where((element) => (element is IpsPatchElement)).Count();
                _fileSize = new FileInfo(file).Length;
                _modified = patches.Where((element) => (element is IpsPatchElement)).Sum(x => ((IpsPatchElement)x).Size);
                try
                {
                    _modified += ((IpsResizeValueElement)patches.Where((element) => (element is IpsResizeValueElement)).First()).GetIntValue();
                }
                catch
                {

                }
                fastObjectListViewRows.SetObjects(patches);
                fastObjectListViewRows.SelectedIndex = 0;
                this.Text = string.Format("{0} - {1}", Application.ProductName, Path.GetFileName(file));

                this.closeToolStripMenuItem.Enabled = true;
                this.closeToolStripButton.Enabled = true;

                exportToolStripButton.Enabled = true;
                exportToolStripMenuItem.Enabled = true;

                goToRowToolStripMenuItem.Enabled = true;
                toolStripButtonGoToRow.Enabled = true;

                toolStripStatusLabelModified.Text = string.Format("Modified: {0} bytes", _modified);
                toolStripStatusLabelFileSize.Text = string.Format(Strings.FileSize, _fileSize);
                ToolStripStatusLabelPatchCount.Text = string.Format(Strings.Patches, _patchCount);
            }
            catch (Exception)
            {
                MessageBox.Show(string.Format(Strings.ErrorFileLoadFailed, file));
            }
        }

        private void LoadSettings()
        {
            OptionsManager.Load(optionsPath, new OptionsModel(this.Width, this.Height, this.Top, this.Left, splitContainer1.SplitterDistance, true, true, true, this.fastObjectListViewRows.SaveState()));
            this.Size = new Size(OptionsManager.FormWidth, OptionsManager.FormHeight);
            toolbarToolStripMenuItem.Checked = OptionsManager.ToolBarVisible;
            dataViewToolStripMenuItem.Checked = OptionsManager.DataViewVisible;
            stringViewToolStripMenuItem.Checked = OptionsManager.StringViewVisible;
            this.Top = OptionsManager.FormTop;
            this.Left = OptionsManager.FormLeft;
            splitContainer1.SplitterDistance = OptionsManager.PanelHeight;

            if (OptionsManager.ListView != null)
            {
                try
                {
                    this.fastObjectListViewRows.RestoreState(OptionsManager.ListView);
                }
                catch
                {

                }
            }

        }

        private void SaveSettings()
        {
            OptionsManager.DataViewVisible = dataViewToolStripMenuItem.Checked;
            OptionsManager.StringViewVisible = stringViewToolStripMenuItem.Checked;
            OptionsManager.ToolBarVisible = toolbarToolStripMenuItem.Checked;
            OptionsManager.PanelHeight = splitContainer1.SplitterDistance;
            OptionsManager.FormTop = this.Top;
            OptionsManager.FormLeft = this.Left;
            OptionsManager.FormWidth = this.Width;
            OptionsManager.FormHeight = this.Height;
            OptionsManager.ListView = this.fastObjectListViewRows.SaveState();
            OptionsManager.Save();
        }
        private void GoToRow()
        {
            string result = string.Empty;
            if (InputBox.Show(this, "Go To Row", "Enter a row number to go to:", ref result) == System.Windows.Forms.DialogResult.OK)
            {
                int row;
                if (int.TryParse(result, out row))
                {
                    row--;
                    fastObjectListViewRows.SelectedIndex = row;
                    fastObjectListViewRows.TopItemIndex = row;
                }
            }
        }
        private void CopyRow()
        {
            fastObjectListViewRows.IncludeColumnHeadersInCopy = true;
            fastObjectListViewRows.CopySelectionToClipboard();
        }
        #endregion

        public FormMain()
        {
            InitializeComponent();
            SetStrings();
            this.olvColumnEnd.AspectGetter = delegate(object row)
            {
                var value = row as IpsPatchElement;
                if (value != null)
                {
                    return string.Format("{0:X6}", value.End);
                }
                else
                {
                    return string.Empty;
                }
            };
            this.olvColumnEnd.Tag = 6;

            this.olvColumnIpsOffset.AspectGetter = delegate(object row)
            {
                var value = row as IpsElement;
                if (value != null)
                {
                    return string.Format("{0:X8}", value.IpsOffset);
                }
                else
                {
                    return string.Empty;
                }

            };
            this.olvColumnIpsOffset.Tag = 8;

            this.olvColumnIpsEnd.AspectGetter = delegate(object row)
            {
                var value = row as IpsElement;
                if (value != null)
                {
                    return string.Format("{0:X8}", value.IpsEnd);
                }
                else
                {
                    return string.Empty;
                }

            };
            this.olvColumnIpsEnd.Tag = 8;

            this.olvColumnIpsSizeHex.AspectGetter = delegate(object row)
            {
                var value = row as IpsElement;
                if (value != null)
                {

                    return string.Format("{0:X}", value.IpsSize);
                }
                else
                {
                    return string.Empty;
                }
            };
            this.olvColumnIpsSizeHex.Tag = 5;

            this.olvColumnIpsSize.AspectGetter = delegate(object row)
            {
                var value = row as IpsElement;
                if (value != null)
                {
                    return value.IpsSize;
                }
                else
                {
                    return string.Empty;
                }
            };
            this.olvColumnIpsSize.Tag = 8;

            this.olvColumnOffset.AspectGetter = delegate(object row)
            {

                if (row is IpsResizeValueElement)
                {
                    return string.Format("{0:X6}", ((IpsResizeValueElement)row).GetIntValue());
                }
                else if (row is IpsPatchElement)
                {
                    return string.Format("{0:X6}", ((IpsPatchElement)row).Offset);
                }
                else
                { return string.Empty; }
            };
            this.olvColumnOffset.Tag = 6;


            this.olvColumnSizeHex.AspectGetter = delegate(object row)
            {
                var value = row as IpsPatchElement;
                if (value != null)
                {
                    return string.Format("{0:X}", value.Size);
                }
                else
                {
                    return string.Empty;
                }


            };
            this.olvColumnSizeHex.Tag = 4;

            this.olvColumnSize.AspectGetter = delegate(object row)
            {
                var value = row as IpsPatchElement;
                if (value != null)
                {
                    return value.Size;
                }
                else
                {
                    return string.Empty;
                }
            };
            this.olvColumnSize.Tag = 5;

            this.olvColumnType.AspectGetter = delegate(object row)
            {
                string name = string.Empty;
                try
                {
                    name = GetDisplayName(row.GetType());
                }
                catch
                {

                }
                return name;
            };
            this.olvColumnType.Tag = 3;

            this.olvColumnNumber.AspectGetter = delegate(object row)
            {
                string index = (fastObjectListViewRows.IndexOf(row) + 1).ToString();
                this.olvColumnNumber.Tag = Math.Max((int)this.olvColumnNumber.Tag, index.Length);
                return index;
            };
            this.olvColumnNumber.Tag = 0;
            this.fastObjectListViewRows.UseFiltering = true;
            this.closeToolStripMenuItem.Enabled = false;
            this.closeToolStripButton.Enabled = false;
            hexBoxData.LineInfoVisible = true;
            hexBoxData.ColumnInfoVisible = true;
            hexBoxData.VScrollBarVisible = true;
            hexBoxData.StringViewVisible = true;
            hexBoxData.UseFixedBytesPerLine = false;
            hexBoxData.LineInfoVisible = true;


            toolStripStatusLabelRows.Text = string.Format(Strings.Row, 0, 0, 0);
            toolStripStatusLabelModified.Text = string.Format(Strings.Modified, 0);
            ToolStripStatusLabelPatchCount.Text = string.Format(Strings.Patches, _patchCount);

            toolbarToolStripMenuItem.Checked = true;


            dataViewToolStripMenuItem.Checked = true;


            stringViewToolStripMenuItem.Checked = true;

            copyRowToolStripMenuItem.Enabled = false;
            toolStripButtonCopyRow.Enabled = false;

            goToRowToolStripMenuItem.Enabled = false;
            toolStripButtonGoToRow.Enabled = false;

            this.StartPosition = FormStartPosition.Manual;

            exportToolStripButton.Enabled = false;
            exportToolStripMenuItem.Enabled = false;

            fastObjectListViewRows.DefaultRenderer = _highlighter;


            // Try to load a file from the command line (such as a file that was dropped onto the icon).
            try
            {
                string file = Environment.GetCommandLineArgs()[1];
                LoadFile(file);

            }
            catch
            {
            }

            LoadSettings();
        }

        private void openPatchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFile();
        }


        private void objectListView1_SelectionChanged(object sender, EventArgs e)
        {
            if (fastObjectListViewRows.SelectedObjects.Count == 1)
            {
                int size = 0;
                try
                {
                    hexBoxData.LineInfoOffset = (long)((IpsPatchElement)fastObjectListViewRows.SelectedObject).Offset;
                    hexBoxData.ByteProvider = new DynamicByteProvider(((IpsPatchElement)fastObjectListViewRows.SelectedObject).GetData());


                    size = ((IpsPatchElement)fastObjectListViewRows.SelectedObject).Size;
                }
                catch
                {
                    hexBoxData.ByteProvider = null;
                }
                finally
                {
                    try
                    {
                        toolStripStatusLabelRows.Text = string.Format(Strings.Row, fastObjectListViewRows.SelectedIndex + 1, fastObjectListViewRows.Items.Count, size);
                    }
                    catch
                    {
                        toolStripStatusLabelRows.Text = string.Empty;
                    }
                    copyRowToolStripMenuItem.Enabled = true;
                    toolStripButtonCopyRow.Enabled = true;
                }
            }
            else
            {
                copyRowToolStripMenuItem.Enabled = false;
                toolStripButtonCopyRow.Enabled = false;
                toolStripStatusLabelRows.Text = "";
            }

        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CloseFile();
        }


        private void openPatchToolStripButton_Click(object sender, EventArgs e)
        {
            OpenFile();
        }



        private void closeToolStripButton_Click(object sender, EventArgs e)
        {
            CloseFile();
        }

        private void exportToolStripButton_Click(object sender, EventArgs e)
        {
            this.Enabled = false;
            ExportFile();
            this.Enabled = true;
        }


        private void toolbarToolStripMenuItem_CheckStateChanged(object sender, EventArgs e)
        {
            toolStrip1.Visible = toolbarToolStripMenuItem.Checked;
        }

        private void dataViewToolStripMenuItem_CheckStateChanged(object sender, EventArgs e)
        {
            splitContainer1.Panel2Collapsed = !dataViewToolStripMenuItem.Checked;
            stringViewToolStripMenuItem.Enabled = dataViewToolStripMenuItem.Checked;
        }

        private void FormMain_DragDrop(object sender, DragEventArgs e)
        {

            try
            {
                Array data = (Array)e.Data.GetData(DataFormats.FileDrop);
                if ((data != null))
                {
                    var file = data.GetValue(0).ToString();

                    this.BeginInvoke((Action<string>)((string value) => { LoadFile(value); }), new object[] { file });

                    this.Activate();
                }

            }
            catch (Exception)
            {
            }
        }

        private void FormMain_DragEnter(object sender, DragEventArgs e)
        {

            if ((e.Data.GetDataPresent(DataFormats.FileDrop)))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void FormMain_Load(object sender, EventArgs e)
        {

        }


        private void filterToolStripTextBox_TextChanged(object sender, EventArgs e)
        {
            if (filterToolStripTextBox.TextLength == 0)
            {
                var filter = TextMatchFilter.Contains(this.fastObjectListViewRows, string.Empty);
                _highlighter.Filter = filter;
                fastObjectListViewRows.ModelFilter = filter;
                fastObjectListViewRows.Refresh();
            }
        }

        private void stringViewToolStripMenuItem_CheckStateChanged(object sender, EventArgs e)
        {
            hexBoxData.StringViewVisible = stringViewToolStripMenuItem.Checked;
        }

        private void filterToolStripTextBox_Enter(object sender, EventArgs e)
        {
            // Kick off SelectAll asyncronously so that it occurs after Click
            BeginInvoke((Action)delegate
            {
                filterToolStripTextBox.SelectAll();
            });
        }

        private void officialForumToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenPage("http://www.codeisle.com/forum/product/ips-peek/");
        }

        private void iPSPeekHomeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenPage("http://www.codeisle.com/");
        }

        private void helpContentsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenPage("http://help.codeisle.com/ips-peek/");
        }

        private void exportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Enabled = false;
            ExportFile();
            this.Enabled = true;
        }

        private void aboutIPSPeekToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (FormAbout about = new FormAbout())
            {
                about.StartPosition = FormStartPosition.CenterParent;
                about.ShowDialog(this);
            }
        }

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.olvColumnIpsOffset.AspectGetter = null;
            this.olvColumnIpsEnd.AspectGetter = null;
            this.olvColumnIpsSizeHex.AspectGetter = null;
            this.olvColumnOffset.AspectGetter = null;

            SaveSettings();
        }

        private void filterToolStripTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                var filter = TextMatchFilter.Contains(this.fastObjectListViewRows, filterToolStripTextBox.Text);
                _highlighter.Filter = filter;
                fastObjectListViewRows.ModelFilter = filter;
                fastObjectListViewRows.Refresh();
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void toolStripButtonGoToRow_Click(object sender, EventArgs e)
        {
            GoToRow();
        }


        private void toolStripButtonCopyRow_Click(object sender, EventArgs e)
        {
            CopyRow();
        }

        private void goToRowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GoToRow();
        }

        private void copyRowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopyRow();
        }
    }
}
