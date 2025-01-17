﻿using avaness.PluginLoader.Data;
using Sandbox;
using Sandbox.Game.Gui;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using avaness.PluginLoader.Stats;
using avaness.PluginLoader.Stats.Model;
using VRage;
using VRage.Audio;
using VRage.Game;
using VRage.Input;
using VRage.Utils;
using VRageMath;
using static Sandbox.Graphics.GUI.MyGuiScreenMessageBox;
using ParallelTasks;

namespace avaness.PluginLoader.GUI
{
    public class MyGuiScreenPluginConfig : MyGuiScreenBase
    {
        private const float BarWidth = 0.85f;
        private const float Spacing = 0.0175f;

        private readonly Dictionary<string, MyGuiControlCheckbox> pluginCheckboxes = new();
        private readonly PluginDetailsPanel pluginDetails;

        private MyGuiControlTable pluginTable;
        private MyGuiControlLabel pluginCountLabel;
        private MyGuiControlButton buttonMore;
        private MyGuiControlContextMenu contextMenu;
        private MyGuiControlContextMenu pluginContextMenu;

        private static PluginConfig Config => Main.Instance.Config;
        private string[] tableFilter;

        public readonly Dictionary<string, bool> AfterRebootEnableFlags = new();
        private bool forceRestart = false;
        public PluginStats PluginStats;

        private PluginData SelectedPlugin
        {
            get => pluginDetails.Plugin;
            set => pluginDetails.Plugin = value;
        }

        private static bool allItemsVisible = true;

        #region Icons

        // Source: MyTerminalControlPanel
        private static readonly MyGuiHighlightTexture IconHide = new()
        {
            Normal = "Textures\\GUI\\Controls\\button_hide.dds",
            Highlight = "Textures\\GUI\\Controls\\button_hide.dds",
            Focus = "Textures\\GUI\\Controls\\button_hide_focus.dds",
            SizePx = new Vector2(40f, 40f)
        };

        // Source: MyTerminalControlPanel
        private static readonly MyGuiHighlightTexture IconShow = new()
        {
            Normal = "Textures\\GUI\\Controls\\button_unhide.dds",
            Highlight = "Textures\\GUI\\Controls\\button_unhide.dds",
            Focus = "Textures\\GUI\\Controls\\button_unhide_focus.dds",
            SizePx = new Vector2(40f, 40f)
        };

        #endregion

        public static void OpenMenu()
        {
            if (Main.Instance.List.HasError)
            {
                MyGuiSandbox.AddScreen(MyGuiSandbox.CreateMessageBox(buttonType: MyMessageBoxButtonsType.OK, messageText: new StringBuilder("An error occurred while downloading the plugin list.\nPlease send your game log to the developers of Plugin Loader."), messageCaption: MyTexts.Get(MyCommonTexts.MessageBoxCaptionError), callback: (x) => MyGuiSandbox.AddScreen(new MyGuiScreenPluginConfig())));
            }
            else
            {
                MyGuiSandbox.AddScreen(new MyGuiScreenPluginConfig());
            }
        }

        /// <summary>
        /// The plugins screen, the constructor itself sets up the menu properties.
        /// </summary>
        private MyGuiScreenPluginConfig() : base(new Vector2(0.5f, 0.5f), MyGuiConstants.SCREEN_BACKGROUND_COLOR, new Vector2(1f, 0.97f), false, null, MySandboxGame.Config.UIBkOpacity, MySandboxGame.Config.UIOpacity)
        {
            EnabledBackgroundFade = true;
            m_closeOnEsc = true;
            m_drawEvenWithoutFocus = true;
            CanHideOthers = true;
            CanBeHidden = true;
            CloseButtonEnabled = true;

            foreach (var plugin in Main.Instance.List)
                AfterRebootEnableFlags[plugin.Id] = plugin.Enabled;

            pluginDetails = new PluginDetailsPanel(this);
        }

        public override string GetFriendlyName()
        {
            return "MyGuiScreenPluginConfig";
        }

        public override void LoadContent()
        {
            base.LoadContent();
            RecreateControls(true);
            PlayerConsent.OnConsentChanged += OnConsentChanged;
        }

        public override void HandleUnhandledInput(bool receivedFocusInThisUpdate)
        {
            var input = MyInput.Static;
            if(input.IsNewKeyPressed(MyKeys.F5) && input.IsAnyAltKeyPressed() && input.IsAnyCtrlKeyPressed())
                Patch.Patch_IngameRestart.ShowRestartMenu();
        }

        public override void UnloadContent()
        {
            PlayerConsent.OnConsentChanged -= OnConsentChanged;
            pluginDetails.OnPluginToggled -= EnablePlugin;
            base.UnloadContent();
        }

        private void OnConsentChanged()
        {
            DownloadStats();
        }

        private void DownloadStats()
        {
            LogFile.WriteLine("Downloading user statistics", false);
            Parallel.Start(() =>
            {
                PluginStats = StatsClient.DownloadStats();
            }, OnDownloadedStats);
        }

        private void OnDownloadedStats()
        {
            pluginDetails?.LoadPluginData();
        }

        /// <summary>
        /// Initializes the controls of the menu on the left side of the menu.
        /// </summary>
        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);

            var title = AddCaption("Plugins List");

            // Sets the origin relative to the center of the caption on the X axis and to the bottom the caption on the y axis.
            var origin = title.Position += new Vector2(0f, title.Size.Y / 2);

            origin.Y += Spacing;

            // Adds a bar right below the caption.
            var titleBar = new MyGuiControlSeparatorList();
            titleBar.AddHorizontal(new Vector2(origin.X - (BarWidth / 2), origin.Y), BarWidth);
            Controls.Add(titleBar);

            origin.Y += Spacing;

            // Change the position of this to move the entire middle section of the menu, the menu bars, menu title, and bottom buttons won't move
            // Adds a search bar right below the bar on the left side of the menu.
            var searchBox = new MyGuiControlSearchBox(new Vector2(origin.X - (BarWidth / 2), origin.Y), originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP);

            // Changing the search box X size will change the plugin list length.
            searchBox.Size = new Vector2(0.4f, searchBox.Size.Y);
            searchBox.OnTextChanged += SearchBox_TextChanged;
            Controls.Add(searchBox);

            #region Visibility Button

            // Adds a button to show only enabled plugins. Located right of the search bar.
            var buttonVisibility = new MyGuiControlButton(new Vector2(origin.X - (BarWidth / 2) + searchBox.Size.X, origin.Y) + new Vector2(0.003f, 0.002f), MyGuiControlButtonStyleEnum.Rectangular, new Vector2(searchBox.Size.Y * 2.52929769833f), onButtonClick: OnVisibilityClick, originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP, toolTip: "Show only enabled plugins.", buttonScale: 0.5f);

            if (allItemsVisible || Config.Count == 0)
            {
                allItemsVisible = true;
                buttonVisibility.Icon = IconHide;
            }
            else
            {
                buttonVisibility.Icon = IconShow;
            }

            Controls.Add(buttonVisibility);

            #endregion

            origin.Y += searchBox.Size.Y + Spacing;

            #region Plugin List

            // Adds the plugin list on the right of the menu below the search bar.
            pluginTable = new MyGuiControlTable
            {
                Position = new Vector2(origin.X - (BarWidth / 2), origin.Y),
                Size = new Vector2(searchBox.Size.X + buttonVisibility.Size.X + 0.001f, 0.6f), // The y value can be bigger than the visible rows count as the visibleRowsCount controls the height.
                OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP,
                ColumnsCount = 3,
                VisibleRowsCount = 20
            };

            pluginTable.SetCustomColumnWidths(new[]
            {
                0.22f,
                0.6f,
                0.22f
            });

            pluginTable.SetColumnName(0, new StringBuilder("Source"));
            pluginTable.SetColumnComparison(0, CellTextOrDataComparison);
            pluginTable.SetColumnName(1, new StringBuilder("Name"));
            pluginTable.SetColumnComparison(1, CellTextComparison);
            pluginTable.SetColumnName(2, new StringBuilder("Enable"));
            pluginTable.SetColumnComparison(2, CellTextComparison);

            // Default sorting
            pluginTable.SortByColumn(2, MyGuiControlTable.SortStateEnum.Ascending);

            // Selecting list items load their details in OnItemSelected
            pluginTable.ItemSelected += OnItemSelected;
            Controls.Add(pluginTable);

            // Double clicking list items toggles the enable flag
            pluginTable.ItemDoubleClicked += OnItemDoubleClicked;

            #endregion

            origin.Y += Spacing + pluginTable.Size.Y;

            // Adds the bar at the bottom between just above the buttons.
            var bottomBar = new MyGuiControlSeparatorList();
            bottomBar.AddHorizontal(new Vector2(origin.X - (BarWidth / 2), origin.Y), BarWidth);
            Controls.Add(bottomBar);

            origin.Y += Spacing;

            // Adds buttons at bottom of menu
            var buttonRestart = new MyGuiControlButton(origin, MyGuiControlButtonStyleEnum.Default, null, null, MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_TOP, "Restart the game and apply changes.", new StringBuilder("Apply"), 0.8f, MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER, MyGuiControlHighlightType.WHEN_ACTIVE, OnRestartButtonClick);
            var buttonClose = new MyGuiControlButton(origin, MyGuiControlButtonStyleEnum.Default, null, null, MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_TOP, "Closes the dialog without saving changes to plugin selection", new StringBuilder("Cancel"), 0.8f, MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER, MyGuiControlHighlightType.WHEN_ACTIVE, OnCancelButtonClick);
            buttonMore = new MyGuiControlButton(origin, MyGuiControlButtonStyleEnum.Tiny, null, null, MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_TOP, "Advanced", new StringBuilder("..."), 0.8f, MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER, MyGuiControlHighlightType.WHEN_ACTIVE, OnMoreButtonClick);

            // FIXME: Use MyLayoutHorizontal instead
            AlignRow(origin, 0.05f, buttonRestart, buttonClose);
            Controls.Add(buttonRestart);
            Controls.Add(buttonClose);
            buttonMore.Position = buttonClose.Position + new Vector2(buttonClose.Size.X / 2 + 0.05f, 0);
            Controls.Add(buttonMore);

            // Adds a place to show the total amount of plugins and to show the total amount of visible plugins.
            pluginCountLabel = new MyGuiControlLabel(new Vector2(origin.X - (BarWidth / 2), buttonRestart.Position.Y), originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP);
            Controls.Add(pluginCountLabel);

            // Right side panel showing the details of the selected plugin
            var rightSideOrigin = buttonVisibility.Position + new Vector2(Spacing * 1.778f + (buttonVisibility.Size.X / 2), -(buttonVisibility.Size.Y / 2));
            pluginDetails.CreateControls(rightSideOrigin);
            Controls.Add(pluginDetails);
            pluginDetails.OnPluginToggled += EnablePlugin;

            // Context menu for the more (...) button
            contextMenu = new MyGuiControlContextMenu();
            contextMenu.Deactivate();
            contextMenu.CreateNewContextMenu();
            contextMenu.AddItem(new StringBuilder("Add development folder"), "Open and compile a folder for development", userData: nameof(OnLoadFolder));
            contextMenu.AddItem(new StringBuilder("Save profile"), "Saved the current plugin selection", userData: nameof(OnSaveProfile));
            contextMenu.AddItem(new StringBuilder("Load profile"), "Loads a saved plugin selection", userData: nameof(OnLoadProfile));
            contextMenu.AddItem(new StringBuilder("------------"));
            contextMenu.AddItem(
                new StringBuilder(PlayerConsent.ConsentGiven ? "Revoke consent" : "Give consent"),
                PlayerConsent.ConsentGiven ? "Revoke consent to data handling, clear my votes" : "Give consent to data handling, allow me to vote",
                userData: nameof(OnConsent));
            contextMenu.Enabled = true;
            contextMenu.ItemClicked += OnContextMenuItemClicked;
            contextMenu.OnDeactivated += OnContextMenuDeactivated;
            // contextMenu.SetMaxSize(new Vector2(0.2f, 0.7f));
            Controls.Add(contextMenu);

            // Context menu for the plugin list
            pluginContextMenu = new MyGuiControlContextMenu();
            pluginContextMenu.Deactivate();
            pluginContextMenu.CreateNewContextMenu();
            pluginContextMenu.ItemClicked += OnPluginContextMenuItemClicked;
            pluginContextMenu.OnDeactivated += OnContextMenuDeactivated;
            Controls.Add(pluginContextMenu);

            // Refreshes the table to show plugins on plugin list
            RefreshTable();

            DownloadStats();
        }

        public void RequireRestart()
        {
            forceRestart = true;
        }

        private void OnLoadFolder()
        {
            LocalFolderPlugin.CreateNew((plugin) =>
            {
                Config.PluginFolders[plugin.Id] = plugin.FolderSettings;
                CreatePlugin(plugin);
            });
        }

        public void CreatePlugin(PluginData data)
        {
            Main.Instance.List.Add(data);
            AfterRebootEnableFlags[data.Id] = true;
            Config.SetEnabled(data.Id, true);
            forceRestart = true;
            RefreshTable(tableFilter);
        }

        public void RemovePlugin(PluginData data)
        {
            Main.Instance.List.Remove(data.Id);
            AfterRebootEnableFlags.Remove(data.Id);
            Config.SetEnabled(data.Id, false);
            forceRestart = true;
            RefreshTable(tableFilter);
        }

        public void RefreshSidePanel()
        {
            pluginDetails?.LoadPluginData();
        }

        /// <summary>
        /// Event that triggers when the visibility button is clicked. This method shows all plugins or only enabled plugins.
        /// </summary>
        /// <param name="btn">The button to assign this event to.</param>
        private void OnVisibilityClick(MyGuiControlButton btn)
        {
            if (allItemsVisible)
            {
                allItemsVisible = false;
                btn.Icon = IconShow;
            }
            else
            {
                allItemsVisible = true;
                btn.Icon = IconHide;
            }

            RefreshTable(tableFilter);
        }

        private static int CellTextOrDataComparison(MyGuiControlTable.Cell x, MyGuiControlTable.Cell y)
        {
            int result = TextComparison(x.Text, y.Text);
            if (result != 0)
            {
                return result;
            }

            return TextComparison((StringBuilder)x.UserData, (StringBuilder)y.UserData);
        }

        private static int CellTextComparison(MyGuiControlTable.Cell x, MyGuiControlTable.Cell y)
        {
            return TextComparison(x.Text, y.Text);
        }

        private static int TextComparison(StringBuilder x, StringBuilder y)
        {
            if (x == null)
            {
                if (y == null)
                    return 0;
                return 1;
            }

            if (y == null)
                return -1;

            return x.CompareTo(y);
        }

        /// <summary>
        /// Clears the table and adds the list of plugins and their information.
        /// </summary>
        /// <param name="filter">Text filter</param>
        private void RefreshTable(string[] filter = null)
        {
            pluginTable.Clear();
            pluginTable.Controls.Clear();
            pluginCheckboxes.Clear();
            var list = Main.Instance.List;
            var noFilter = filter == null || filter.Length == 0;
            foreach (var plugin in list)
            {
                var enabled = AfterRebootEnableFlags[plugin.Id];

                if (noFilter && (plugin.Hidden || !allItemsVisible) && !enabled)
                    continue;

                if (!noFilter && !FilterName(plugin.FriendlyName, filter))
                    continue;

                var row = new MyGuiControlTable.Row(plugin);
                pluginTable.Add(row);

                var name = new StringBuilder(plugin.FriendlyName);
                row.AddCell(new MyGuiControlTable.Cell(plugin.Source, name));

                var tip = plugin.FriendlyName;
                if (!string.IsNullOrWhiteSpace(plugin.Tooltip))
                    tip += "\n" + plugin.Tooltip;
                row.AddCell(new MyGuiControlTable.Cell(plugin.FriendlyName, toolTip: tip));

                var text = new StringBuilder(FormatCheckboxSortKey(plugin, enabled));
                var enabledCell = new MyGuiControlTable.Cell(text, name);
                var enabledCheckbox = new MyGuiControlCheckbox(isChecked: enabled)
                {
                    UserData = plugin,
                    Visible = true
                };
                enabledCheckbox.IsCheckedChanged += OnPluginCheckboxChanged;
                enabledCell.Control = enabledCheckbox;
                pluginTable.Controls.Add(enabledCheckbox);
                pluginCheckboxes[plugin.Id] = enabledCheckbox;
                row.AddCell(enabledCell);
            }

            pluginCountLabel.Text = pluginTable.RowsCount + "/" + list.Count + " visible";
            pluginTable.Sort(false);
            pluginTable.SelectedRowIndex = null;
            tableFilter = filter;
            pluginTable.SelectedRowIndex = 0;

            var args = new MyGuiControlTable.EventArgs { RowIndex = 0 };
            OnItemSelected(pluginTable, args);
        }

        private static string FormatCheckboxSortKey(PluginData plugin, bool enabled)
        {
            // Uses a prefix of + and - to list plugins to enable to the top
            return enabled ? $"+{plugin.FriendlyName}|{plugin.Source}" : $"-{plugin.FriendlyName}|{plugin.Source}";
        }

        /// <summary>
        /// Event that triggers when the text in the searchbox is changed.
        /// </summary>
        /// <param name="txt">The text that was entered into the searchbox.</param>
        private void SearchBox_TextChanged(string txt)
        {
            string[] args = txt.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            RefreshTable(args);
        }

        private static bool FilterName(string name, IEnumerable<string> filter)
        {
            return filter.All(s => name.Contains(s, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Sets text on right side of screen.
        /// </summary>
        /// <param name="table">Table to get the plugin data.</param>
        /// <param name="args">Event arguments.</param>
        private void OnItemSelected(MyGuiControlTable table, MyGuiControlTable.EventArgs args)
        {
            if (!TryGetPluginByRowIndex(args.RowIndex, out var plugin))
                return;

            if (args.MouseButton == MyMouseButtonsEnum.Right && plugin.OpenContextMenu(pluginContextMenu))
            {
                pluginContextMenu.ItemList_UseSimpleItemListMouseOverCheck = true;
                pluginContextMenu.Activate();
            }

            contextMenu.Deactivate();
            SelectedPlugin = plugin;
        }

        private void OnItemDoubleClicked(MyGuiControlTable table, MyGuiControlTable.EventArgs args)
        {
            if (!TryGetPluginByRowIndex(args.RowIndex, out var data))
                return;

            EnablePlugin(data, !AfterRebootEnableFlags[data.Id]);
        }

        private bool TryGetPluginByRowIndex(int rowIndex, out PluginData plugin)
        {
            if (rowIndex < 0 || rowIndex >= pluginTable.RowsCount)
            {
                plugin = null;
                return false;
            }

            var row = pluginTable.GetRow(rowIndex);
            plugin = row.UserData as PluginData;
            return plugin != null;
        }

        private void AlignRow(Vector2 origin, float spacing, params MyGuiControlBase[] elements)
        {
            if (elements.Length == 0)
                return;

            float totalWidth = 0;
            for (int i = 0; i < elements.Length; i++)
            {
                MyGuiControlBase btn = elements[i];
                totalWidth += btn.Size.X;
                if (i < elements.Length - 1)
                    totalWidth += spacing;
            }

            float originX = origin.X - (totalWidth / 2);
            foreach (MyGuiControlBase btn in elements)
            {
                float halfWidth = btn.Size.X / 2;
                originX += halfWidth;
                btn.Position = new Vector2(originX, origin.Y);
                originX += spacing + halfWidth;
            }
        }

        private void OnPluginCheckboxChanged(MyGuiControlCheckbox checkbox)
        {
            var plugin = (PluginData)checkbox.UserData;
            EnablePlugin(plugin, checkbox.IsChecked);

            if (ReferenceEquals(plugin, SelectedPlugin))
                pluginDetails.LoadPluginData();
        }

        public void EnablePlugin(PluginData plugin, bool enable)
        {
            if (enable == AfterRebootEnableFlags[plugin.Id])
                return;

            AfterRebootEnableFlags[plugin.Id] = enable;

            SetPluginCheckbox(plugin, enable);

            if (enable)
            {
                DisableOtherPluginsInSameGroup(plugin);
                EnableDependencies(plugin);
            }
        }

        private void SetPluginCheckbox(PluginData plugin, bool enable)
        {
            if (!pluginCheckboxes.TryGetValue(plugin.Id, out MyGuiControlCheckbox checkbox))
                return; // The checkbox might not exist if the target plugin is a dependency not currently in the table
            checkbox.IsChecked = enable;

            var row = pluginTable.Find(x => ReferenceEquals(x.UserData as PluginData, plugin));
            row?.GetCell(2).Text.Clear().Append(FormatCheckboxSortKey(plugin, enable));
        }

        private void DisableOtherPluginsInSameGroup(PluginData plugin)
        {
            foreach (PluginData other in plugin.Group)
            {
                if (!ReferenceEquals(other, plugin))
                    EnablePlugin(other, false);
            }
        }

        private void EnableDependencies(PluginData plugin)
        {
            if (plugin is not ModPlugin mod || mod.Dependencies == null)
                return;

            foreach (PluginData other in mod.Dependencies)
            {
                if (!ReferenceEquals(other, plugin))
                    EnablePlugin(other, true);
            }
        }

        private void OnCancelButtonClick(MyGuiControlButton btn)
        {
            CloseScreen();
        }

        private void OnMoreButtonClick(MyGuiControlButton _)
        {
            contextMenu.ItemList_UseSimpleItemListMouseOverCheck = true;
            contextMenu.Enabled = false;
            contextMenu.Activate(false);
            contextMenu.OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP;
            contextMenu.Position = buttonMore.Position + buttonMore.Size * new Vector2(-1.3f, -1.9f);
            FocusContextMenuList();
        }

        private void FocusContextMenuList()
        {
            var guiControlsOwner = (IMyGuiControlsOwner)contextMenu;
            while (guiControlsOwner.Owner != null)
            {
                guiControlsOwner = guiControlsOwner.Owner;
                if (guiControlsOwner is not MyGuiScreenBase myGuiScreenBase)
                    continue;

                myGuiScreenBase.FocusedControl = contextMenu.GetInnerList();
                break;
            }
        }

        private void OnContextMenuDeactivated()
        {
            contextMenu.Enabled = true;
        }

        private void OnContextMenuItemClicked(MyGuiControlContextMenu _, MyGuiControlContextMenu.EventArgs args)
        {
            contextMenu.Deactivate();

            switch ((string)args.UserData)
            {
                case nameof(OnLoadFolder):
                    OnLoadFolder();
                    break;

                case nameof(OnSaveProfile):
                    OnSaveProfile();
                    break;

                case nameof(OnLoadProfile):
                    OnLoadProfile();
                    break;

                case nameof(OnConsent):
                    OnConsent();
                    break;
            }
        }

        private void OnPluginContextMenuItemClicked(MyGuiControlContextMenu menu, MyGuiControlContextMenu.EventArgs args)
        {
            SelectedPlugin?.ContextMenuClicked(this, args);
        }

        private void OnSaveProfile()
        {
            var timestamp = DateTime.Now.ToString("O").Substring(0, 19).Replace('T', ' ');
            MyGuiSandbox.AddScreen(new NameDialog(OnProfileNameProvided, "Save profile", timestamp));
        }

        private void OnProfileNameProvided(string name)
        {
            var afterRebootEnablePluginIds = AfterRebootEnableFlags
                .Where(p => p.Value)
                .Select(p => p.Key);

            var profile = new Profile(name, afterRebootEnablePluginIds.ToArray());
            Config.ProfileMap[profile.Key] = profile;
            Config.Save();
        }

        private void OnLoadProfile()
        {
            MyGuiSandbox.AddScreen(new ProfilesDialog("Load profile", OnProfileLoaded));
        }

        private void OnProfileLoaded(Profile profile)
        {
            var pluginsEnabledInProfile = profile.Plugins.ToHashSet();

            foreach (var plugin in Main.Instance.List)
                EnablePlugin(plugin, pluginsEnabledInProfile.Contains(plugin.Id));

            pluginTable.SortByColumn(2, MyGuiControlTable.SortStateEnum.Ascending);
        }

        private void OnConsent()
        {
            PlayerConsent.ShowDialog();
        }

        private bool RequiresRestart => forceRestart || Main.Instance.List.Any(plugin => plugin.Enabled != AfterRebootEnableFlags[plugin.Id]);

        private void OnRestartButtonClick(MyGuiControlButton btn)
        {
            if (!RequiresRestart)
            {
                CloseScreen();
                return;
            }

            MyGuiSandbox.AddScreen(MyGuiSandbox.CreateMessageBox(MyMessageBoxStyleEnum.Info, MyMessageBoxButtonsType.YES_NO_CANCEL, new StringBuilder("A restart is required to apply changes. Would you like to restart the game now?"), new StringBuilder("Apply Changes?"), callback: AskRestartResult));
        }

        private void Save()
        {
            if (!RequiresRestart)
                return;

            foreach (var plugin in Main.Instance.List)
                Config.SetEnabled(plugin.Id, AfterRebootEnableFlags[plugin.Id]);

            Config.Save();
        }

        #region Restart

        private void AskRestartResult(ResultEnum result)
        {
            if (result == ResultEnum.YES)
            {
                Save();
                if (MyGuiScreenGamePlay.Static != null)
                {
                    ShowSaveMenu(delegate { LoaderTools.UnloadAndRestart(); });
                    return;
                }

                LoaderTools.UnloadAndRestart();
            }
            else if (result == ResultEnum.NO)
            {
                Save();
                CloseScreen();
            }
        }

        /// <summary>
        /// From WesternGamer/InGameWorldLoading
        /// </summary>
        /// <param name="afterMenu">Action after code is executed.</param>
        private static void ShowSaveMenu(Action afterMenu)
        {
            // Sync.IsServer is backwards
            if (!Sync.IsServer)
            {
                afterMenu();
                return;
            }

            string message = "";
            bool isCampaign = false;
            MyMessageBoxButtonsType buttonsType = MyMessageBoxButtonsType.YES_NO_CANCEL;

            // Sync.IsServer is backwards
            if (Sync.IsServer && !MySession.Static.Settings.EnableSaving)
            {
                message += "Are you sure that you want to restart the game? All progress from the last checkpoint will be lost.";
                isCampaign = true;
                buttonsType = MyMessageBoxButtonsType.YES_NO;
            }
            else
            {
                message += "Save changes before restarting game?";
            }

            MyGuiScreenMessageBox saveMenu = MyGuiSandbox.CreateMessageBox(buttonType: buttonsType, messageText: new StringBuilder(message), messageCaption: MyTexts.Get(MyCommonTexts.MessageBoxCaptionPleaseConfirm), callback: ShowSaveMenuCallback, cancelButtonText: MyStringId.GetOrCompute("Don't Restart"));
            saveMenu.InstantClose = false;
            MyGuiSandbox.AddScreen(saveMenu);

            void ShowSaveMenuCallback(ResultEnum callbackReturn)
            {
                if (isCampaign)
                {
                    if (callbackReturn == ResultEnum.YES)
                        afterMenu();

                    return;
                }

                switch (callbackReturn)
                {
                    case ResultEnum.YES:
                        MyAsyncSaving.Start(delegate { MySandboxGame.Static.OnScreenshotTaken += UnloadAndExitAfterScreenshotWasTaken; });
                        break;

                    case ResultEnum.NO:
                        MyAudio.Static.Mute = true;
                        MyAudio.Static.StopMusic();
                        afterMenu();
                        break;
                }
            }

            void UnloadAndExitAfterScreenshotWasTaken(object sender, EventArgs e)
            {
                MySandboxGame.Static.OnScreenshotTaken -= UnloadAndExitAfterScreenshotWasTaken;
                afterMenu();
            }
        }

        #endregion
    }
}