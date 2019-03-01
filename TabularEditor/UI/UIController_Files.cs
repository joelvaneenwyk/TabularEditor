﻿using Microsoft.WindowsAPICodePack.Dialogs;
using Microsoft.WindowsAPICodePack.Dialogs.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TabularEditor.TOMWrapper;
using TabularEditor.TOMWrapper.Serialization;
using TabularEditor.TOMWrapper.Utils;
using TabularEditor.UIServices;

namespace TabularEditor.UI
{
    public partial class UIController
    {
        public void File_Open(string fileName)
        {
            var oldFile = File_Current;
            var oldLastWrite = File_LastWrite;
            var oldSaveMode = File_SaveMode;
            var oldHandler = Handler;

            var cancel = false;

            using (new Hourglass())
            {
                try
                {
                    Handler = new TabularModelHandler(fileName, Preferences.Current.GetSettings());

                    if(Handler.SourceType == ModelSourceType.Pbit)
                    {
                        var msg = Preferences.Current.AllowUnsupportedPBIFeatures ?
                            "You have selected a Power BI Template (.pbit) file.\n\nEditing the Data Model of a .pbit file inside Tabular Editor may cause issues when subsequently loading the file in Power BI Desktop.\n\nMake sure to keep a backup of the file, and proceed at your own risk." :
                            "You have selected a Power BI Template (.pbit) file.\n\nEditing the Data Model of a .pbit file inside Tabular Editor may cause issues when subsequently loading the file in Power BI Desktop. Properties that are known to be unsupported (such as Display Folders) have been disabled, but there is still a risk that certain changes made with Tabular Editor can corrupt the file.\n\nMake sure to keep a backup of the file, and proceed at your own risk.";
                        var mr = MessageBox.Show(msg,
                            "Opening Power BI Template", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                        if(mr == DialogResult.Cancel)
                        {
                            cancel = true;
                        }
                    }

                    if (!cancel)
                    {
                        File_Current = Handler.Source;
                        File_SaveMode = Handler.SourceType;

                        // TODO: Use a FileSystemWatcher to watch for changes to the currently loaded file(s)
                        // and handle them appropriately. For now, we just store the LastWriteDate of the loaded
                        // file, to ensure that we don't accidentally overwrite newer changes when saving.
                        File_LastWrite = File_SaveMode == ModelSourceType.Folder ? GetLastDirChange(File_Current) : File.GetLastWriteTime(File_Current);

                        LoadTabularModelToUI();
                        RecentFiles.Add(fileName);
                        RecentFiles.Save();
                        UI.FormMain.PopulateRecentFilesList();
                    }
                }
                catch (Exception ex)
                {
                    cancel = true;
                    MessageBox.Show(ex.Message, "Error loading Model from disk", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            if(cancel)
            {
                Handler = oldHandler;
                File_Current = oldFile;
                File_SaveMode = oldSaveMode;
                File_LastWrite = oldLastWrite;
            }
        }

        public void File_New()
        {
            var cl = 0;

            var td = new TaskDialog();
            var tdb1200 = new TaskDialogButton("btnCL1200", "CL 1200");
            var tdb1400 = new TaskDialogButton("btnCL1400", "CL 1400");
            var tdbCancel = new TaskDialogButton("btnCancel", "Cancel");
            tdb1200.Click += (s, e) => { cl = 1200;  td.Close(TaskDialogResult.CustomButtonClicked); };
            tdb1400.Click += (s, e) => { cl = 1400;  td.Close(TaskDialogResult.CustomButtonClicked); };
            tdbCancel.Click += (s, e) => { td.Close(TaskDialogResult.Cancel); };

            td.Controls.Add(tdb1200);
            td.Controls.Add(tdb1400);
            td.Controls.Add(tdbCancel);

            td.Caption = "Choose Compatibility Level";
            td.Text = "Which Compatibility Level (1200 or 1400) do you want to use for the new model?";

            var tdr = td.Show();
            if (cl == 0) return;
            
            Handler = new TabularModelHandler(cl, Preferences.Current.GetSettings());
            File_Current = null;
            File_SaveMode = Handler.SourceType;

            LoadTabularModelToUI();
        }

        private void FileNewCompatibilityLevel_Click(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Call this method before calling File_Open() or Database_Connect() to check whether the
        /// currently loaded model has unsaved changes.
        /// </summary>
        /// <returns>True if the currently loaded model has unsaved changed.</returns>
        public bool DiscardChangesCheck()
        {
            if (Handler != null && Handler.HasUnsavedChanges)
            {
                if (MessageBox.Show("You have made changes to the model which have not yet been saved. Continue without saving changes?", "Unsaved changes", MessageBoxButtons.OKCancel, MessageBoxIcon.Question)
                    == DialogResult.Cancel) return true;
            }
            return false;
        }

        private void UndoManager_UndoActionAdded(object sender, EventArgs e)
        {
            UpdateUIText();
        }

        public string File_Current { get; private set; }

        public void File_Open(bool fromFolder = false)
        {
            if (DiscardChangesCheck()) return;

            string fileName;
            if(fromFolder)
            {
                using (var dlg = new CommonOpenFileDialog() { IsFolderPicker = true })
                {
                    if (dlg.ShowDialog() == CommonFileDialogResult.Cancel) return;
                    fileName = dlg.FileName;
                }
            } else
            {
                if (UI.OpenBimDialog.ShowDialog() == DialogResult.Cancel) return;
                fileName = UI.OpenBimDialog.FileName;
            }

            File_Open(fileName);
        }

        private ModelSourceType _file_SaveMode;
        public ModelSourceType File_SaveMode
        {
            get { return _file_SaveMode; }
            private set
            {
                _file_SaveMode = value;

                var actSave = UI.FormMain.actSave;
                switch (_file_SaveMode)
                {
                    case TOMWrapper.ModelSourceType.Database:
                        actSave.Text = "&Save";
                        actSave.ToolTipText = "Saves the changes to the connected database (Ctrl+S)";
                        actSave.Image = Resources.SaveToDB;
                        break;
                    case TOMWrapper.ModelSourceType.File:
                        actSave.Text = "&Save";
                        actSave.ToolTipText = "Saves the changes back to the currently loaded .bim file (Ctrl+S)";
                        actSave.Image = Resources.SaveToFile;
                        break;
                    case TOMWrapper.ModelSourceType.Folder:
                        actSave.Text = "&Save";
                        actSave.ToolTipText = "Saves the changes back to the currently loaded model folder structure (Ctrl+S)";
                        actSave.Image = Resources.SaveFolderTree;
                        break;
                    case TOMWrapper.ModelSourceType.Pbit:
                        actSave.Text = "&Save";
                        actSave.ToolTipText = "Saves the changes back to the currently loaded Power BI Template (Ctrl+S)";
                        actSave.Image = Resources.SaveToPBI;
                        break;
                }
            }
        }
        public DateTime File_LastWrite { get; private set; }

        public void File_SaveAs()
        {
            ExpressionEditor_AcceptEdit();

            // Only show the "Use serialize options from annotations" checkbox when the current model has these annotations,
            // and not when switching between file/folder:
            var showSfa = Handler.HasSerializeOptions;

            // If the model is currently loaded from a database or a folder structure, use the default "Model.bim" as a file
            // name. Otherwise, use the name of the current file:
            var defaultFileName = (Handler.SourceType == ModelSourceType.Database || Handler.SourceType == ModelSourceType.Folder) ? "Model.bim" : File_Current;

            // We only allow saving as a Power BI Template, if the current model was loaded from a template:
            var allowPbit = Handler.SourceType == ModelSourceType.Pbit;

            // This flag indicates whether we're currently connected to a database:
            var connectedToDatabase = Handler.SourceType == ModelSourceType.Database;

            // This flag indicates whether the "Current File" pointer should be set to the new file location, which
            // is the typical behaviour of Windows applications when choosing "Save As...". However, when connected
            // to a database, we do not want to do this, as users will probably want to keep working on the existing
            // connection.
            var changeFilePointer = !connectedToDatabase;

            // This flag indicates whether we should reset the undo-checkpoint after saving the model.
            // The purpose of resetting the checkpoint is to visually indicate to the user, that no
            // changes have been made to the model since the last save. We do this, only when changing
            // the "Current File" pointer:
            var resetCheckPoint = changeFilePointer;

            // This flag indicates whether the SerializationOptions annotations on the currently loaded
            // model will be restored to its original value, after the model is saved (possibly using
            // different serialization options, depending on the other arguments of the Save() method).
            // We should always restore when connected to a database, as we don't want our serialization
            // options to be saved to the database - only to the file/folder that we're currently saving to.
            var restoreSerializationOptions = connectedToDatabase;

            // The serialization options to use when saving (unless users check the "Use serialization settings from annotations" checkbox):
            var serializationOptions = Preferences.Current.GetSerializeOptions();

            using (var dialog = SaveAsDialog.CreateFileDialog(showSfa, defaultFileName, allowPbit))
            {
                var res = dialog.ShowDialog();

                if (res == CommonFileDialogResult.Ok && !string.IsNullOrWhiteSpace(dialog.FileName))
                {
                    using (new Hourglass())
                    {
                        UI.StatusLabel.Text = "Saving...";

                        // Save as a Power BI Template, if that's the type of file chosen in the dialog:
                        var saveFormat = dialog.FileType == "pbit" ? SaveFormat.PowerBiTemplate : SaveFormat.ModelSchemaOnly;

                        Handler.Save(dialog.FileName,
                            saveFormat,
                            serializationOptions,
                            dialog.UseSerializationFromAnnotations,
                            resetCheckPoint,
                            restoreSerializationOptions);

                        RecentFiles.Add(dialog.FileName);
                        RecentFiles.Save();
                        UI.FormMain.PopulateRecentFilesList();

                        // If not connected to a database, change the current working file:
                        if (changeFilePointer)
                        {
                            File_Current = dialog.FileName;
                            File_SaveMode = ModelSourceType.File;
                            Environment.CurrentDirectory = (new FileInfo(File_Current)).DirectoryName;
                        }

                        UpdateUIText();
                    }
                }
            }
        }

        public void File_SaveAs_ToFolder()
        {
            ExpressionEditor_AcceptEdit();

            // Save as a Folder structure:
            var saveFormat = SaveFormat.TabularEditorFolder;

            // This flag indicates whether we're currently connected to a database:
            var connectedToDatabase = Handler.SourceType == ModelSourceType.Database;

            // This flag indicates whether the "Current File" pointer should be set to the new file location, which
            // is the typical behaviour of Windows applications when choosing "Save As...". However, when connected
            // to a database, we do not want to do this, as users will probably want to keep working on the existing
            // connection.
            var changeFilePointer = !connectedToDatabase;

            // This flag indicates whether we should reset the undo-checkpoint after saving the model.
            // The purpose of resetting the checkpoint is to visually indicate to the user, that no
            // changes have been made to the model since the last save. We do this, only when changing
            // the "Current File" pointer:
            var resetCheckPoint = changeFilePointer;

            // This flag indicates whether the SerializationOptions annotations on the currently loaded
            // model will be restored to its original value, after the model is saved (possibly using
            // different serialization options, depending on the other arguments of the Save() method).
            // We should always restore when connected to a database, as we don't want our serialization
            // options to be saved to the database - only to the file/folder that we're currently saving to.
            var restoreSerializationOptions = connectedToDatabase;

            // The serialization options to use when saving (unless users check the "Use serialization settings from annotations" checkbox):
            var serializationOptions = Preferences.Current.GetSerializeOptions();

            // Only show the "Use serialize options from annotations" checkbox when the current model has these annotations:
            var showSfa = Handler.HasSerializeOptions;

            using (var dialog = SaveAsDialog.CreateFolderDialog(showSfa))
            {
                var res = dialog.ShowDialog();
                if(res == CommonFileDialogResult.Ok && !string.IsNullOrWhiteSpace(dialog.FileName))
                {
                    using (new Hourglass())
                    {
                        UI.StatusLabel.Text = "Saving...";

                        Handler.Save(dialog.FileName,
                            saveFormat,
                            serializationOptions,
                            dialog.UseSerializationFromAnnotations,
                            resetCheckPoint,
                            restoreSerializationOptions);

                        RecentFiles.Add(dialog.FileName);
                        RecentFiles.Save();
                        UI.FormMain.PopulateRecentFilesList();

                        // If working with a file, change the current file pointer:
                        if (changeFilePointer)
                        {
                            File_SaveMode = ModelSourceType.Folder;
                            File_Current = dialog.FileName;
                            Environment.CurrentDirectory = File_Current;
                        }

                        UpdateUIText();
                    }
                }
            }
        }

        public void Save()
        {
            ExpressionEditor_AcceptEdit();

            if (File_Current == null && File_SaveMode == ModelSourceType.File)
            {
                File_SaveAs();
                File_LastWrite = DateTime.Now;
                return;
            }

            UI.StatusLabel.Text = "Saving...";
            using (new Hourglass())
            {

                if (File_SaveMode == ModelSourceType.Database)
                {
                    Database_Save();
                }
                else
                {
                    try
                    {
                        DialogResult mr = DialogResult.OK;
                        if (File_SaveMode == ModelSourceType.Folder)
                        {
                            if (GetLastDirChange(File_Current, File_LastWrite) > File_LastWrite)
                            {
                                mr = MessageBox.Show(
                                    "Changes were made to the currently loaded folder structure after the model was loaded in Tabular Editor. Overwrite these changes?", "Overwriting folder structure changes", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                            }
                            if (mr == DialogResult.OK)
                            {
                                Handler.Save(File_Current, SaveFormat.TabularEditorFolder, null, true, true);
                                File_LastWrite = DateTime.Now;
                            }
                        }
                        else
                        {
                            if (File.GetLastWriteTime(File_Current) > File_LastWrite)
                            {
                                mr = MessageBox.Show(
                                    "Changes were made to the currently loaded file after the model was loaded in Tabular Editor. Overwrite these changes?", "Overwriting file changes", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                            }
                            if (mr == DialogResult.OK)
                            {
                                if (File_SaveMode == ModelSourceType.Pbit)
                                    Handler.Save(File_Current, SaveFormat.PowerBiTemplate, SerializeOptions.PowerBi, false, true);
                                else
                                    Handler.Save(File_Current, SaveFormat.ModelSchemaOnly, null, true, true);
                                File_LastWrite = DateTime.Now;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show(e.Message, "Could not save metadata to file", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }

                }
                UpdateUIText();
            }
        }

        private DateTime GetLastDirChange(string path)
        {
            return GetLastDirChange(path, DateTime.MaxValue);
        }

        private DateTime GetLastDirChange(string path, DateTime anyAfter)
        {
            var dirs = Directory.EnumerateFiles(path, "*.json", SearchOption.AllDirectories);
            var maxSoFar = DateTime.MinValue;
            foreach(var dir in dirs)
            {
                var dt = Directory.GetLastWriteTime(dir);
                if (dt > maxSoFar) maxSoFar = dt;
                if (maxSoFar > anyAfter) break;
            }
            return maxSoFar;
        }
    }
}
