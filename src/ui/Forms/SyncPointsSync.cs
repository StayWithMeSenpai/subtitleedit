﻿using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls;
using FuzzySharp;
using System.Security.Policy;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class SyncPointsSync : PositionAndSizeForm
    {
        new public class Anchor : IEquatable<Anchor>
        {
            public int First;
            public int Second;

            public int Collisions;

            public Anchor(int first, int second)
            {
                First = first;
                Second = second;
                Collisions = 0;
            }

            public override bool Equals(object obj) => Equals(obj as Anchor);

            public bool Equals(Anchor other)
            {
                if (other is null) return false;
                if (ReferenceEquals(this, other)) return true; // WHAT THE FUCK IS THIS STACK OVERFLOW?!?!?!??!
                return First == other.First && Second == other.Second;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 23 + First.GetHashCode();
                    hash = hash * 23 + Second.GetHashCode();
                    return hash;
                }
            }

            public override string ToString() => $"({First}, {Second}) [Collisions: {Collisions}]";
        }

        public class ListBoxSyncPoint
        {
            public int Index { get; set; }
            public string Text { get; set; }

            public override string ToString()
            {
                return Text;
            }
        }

        private string _subtitleFileName;
        private int _audioTrackNumber;
        private Subtitle _subtitle;
        private Subtitle _originalSubtitle;
        private Subtitle _otherSubtitle;
        private SortedDictionary<int, (TimeSpan Time, int OtherIndex)> _synchronizationPoints = new SortedDictionary<int, (TimeSpan, int)>();
        private readonly Keys _mainGeneralGoToNextSubtitle = UiUtil.GetKeys(Configuration.Settings.Shortcuts.GeneralGoToNextSubtitle);
        private readonly Keys _mainGeneralGoToNextSubtitlePlayTranslate = UiUtil.GetKeys(Configuration.Settings.Shortcuts.GeneralGoToNextSubtitlePlayTranslate);
        private readonly Keys _mainGeneralGoToPrevSubtitle = UiUtil.GetKeys(Configuration.Settings.Shortcuts.GeneralGoToPrevSubtitle);
        private readonly Keys _mainGeneralGoToPrevSubtitlePlayTranslate = UiUtil.GetKeys(Configuration.Settings.Shortcuts.GeneralGoToPrevSubtitlePlayTranslate);

        public string VideoFileName { get; private set; }

        public Subtitle FixedSubtitle => _subtitle;

        public SyncPointsSync()
        {
            UiUtil.PreInitialize(this);
            InitializeComponent();
            UiUtil.FixFonts(this);

            buttonSetSyncPoint.Text = LanguageSettings.Current.PointSync.SetSyncPoint;
            buttonRemoveSyncPoint.Text = LanguageSettings.Current.PointSync.RemoveSyncPoint;
            buttonOK.Text = LanguageSettings.Current.General.Ok;
            buttonApplySync.Text = LanguageSettings.Current.PointSync.ApplySync;
            buttonCancel.Text = LanguageSettings.Current.General.Cancel;
            labelNoOfSyncPoints.Text = string.Format(LanguageSettings.Current.PointSync.SyncPointsX, 0);
            labelSyncInfo.Text = LanguageSettings.Current.PointSync.Info;
            buttonFindText.Text = LanguageSettings.Current.VisualSync.FindText;
            buttonFindTextOther.Text = LanguageSettings.Current.VisualSync.FindText;
            subtitleListView1.InitializeLanguage(LanguageSettings.Current.General, Configuration.Settings);
            subtitleListView2.InitializeLanguage(LanguageSettings.Current.General, Configuration.Settings);
            subtitleListView1.InitializeTimestampColumnWidths(this);
            subtitleListView2.InitializeTimestampColumnWidths(this);
            UiUtil.InitializeSubtitleFont(subtitleListView1);
            UiUtil.InitializeSubtitleFont(subtitleListView2);
            subtitleListView1.AutoSizeAllColumns(this);
            subtitleListView2.AutoSizeAllColumns(this);
            UiUtil.FixLargeFonts(this, buttonOK);
            labelAdjustFactor.Text = string.Empty;
        }

        public void Initialize(Subtitle subtitle, string subtitleFileName, string videoFileName, int audioTrackNumber)
        {
            Text = LanguageSettings.Current.PointSync.Title;
            labelSubtitleFileName.Text = subtitleFileName;
            _subtitle = new Subtitle(subtitle);
            _originalSubtitle = subtitle;
            _subtitleFileName = subtitleFileName;
            VideoFileName = videoFileName;
            _audioTrackNumber = audioTrackNumber;
            subtitleListView1.Fill(subtitle);
            if (subtitleListView1.Items.Count > 0)
            {
                subtitleListView1.Items[0].Selected = true;
            }

            subtitleListView1.Anchor = AnchorStyles.Left;
            buttonSetSyncPoint.Anchor = AnchorStyles.Left;
            buttonAutoSetSyncPoints.Anchor = AnchorStyles.Left;
            autoSetSyncProgress.Anchor = AnchorStyles.Left;
            similarityScoreLabel.Anchor = AnchorStyles.Left;
            minFuzzScore.Anchor = AnchorStyles.Left;
            minFuzzScore.Anchor = AnchorStyles.Left;
            buttonRemoveSyncPoint.Anchor = AnchorStyles.Left;
            labelNoOfSyncPoints.Anchor = AnchorStyles.Left;
            listBoxSyncPoints.Anchor = AnchorStyles.Left;
            groupBoxImportResult.Anchor = AnchorStyles.Left;
            labelOtherSubtitleFileName.Visible = false;
            subtitleListView2.Visible = false;
            buttonFindTextOther.Visible = false;
            groupBoxImportResult.Width = listBoxSyncPoints.Left + listBoxSyncPoints.Width + 20;
            Width = groupBoxImportResult.Left + groupBoxImportResult.Width + 15;
            subtitleListView1.Anchor = AnchorStyles.Left | AnchorStyles.Bottom | AnchorStyles.Top | AnchorStyles.Right;
            buttonSetSyncPoint.Anchor = AnchorStyles.Right;
            buttonAutoSetSyncPoints.Anchor = AnchorStyles.Right;
            autoSetSyncProgress.Anchor = AnchorStyles.Right;
            similarityScoreLabel.Anchor = AnchorStyles.Right;
            minFuzzScore.Anchor = AnchorStyles.Right;
            buttonRemoveSyncPoint.Anchor = AnchorStyles.Right;
            labelNoOfSyncPoints.Anchor = AnchorStyles.Right;
            listBoxSyncPoints.Anchor = AnchorStyles.Right;
            groupBoxImportResult.Anchor = AnchorStyles.Left | AnchorStyles.Bottom | AnchorStyles.Top | AnchorStyles.Right;
            buttonFindText.Left = subtitleListView1.Left + subtitleListView1.Width - buttonFindText.Width;
            Width = 900;
            groupBoxImportResult.Width = Width - groupBoxImportResult.Left * 3;
            labelAdjustFactor.Left = listBoxSyncPoints.Left;
            labelAdjustFactor.Anchor = listBoxSyncPoints.Anchor;
            MinimumSize = new Size(Width - 50, MinimumSize.Height);
        }

        public void Initialize(Subtitle subtitle, string subtitleFileName, string videoFileName, int audioTrackNumber, string otherSubtitleFileName, Subtitle otherSubtitle)
        {
            Text = LanguageSettings.Current.PointSync.TitleViaOtherSubtitle;
            labelSubtitleFileName.Text = subtitleFileName;
            _subtitle = new Subtitle(subtitle);
            _otherSubtitle = otherSubtitle;
            _originalSubtitle = subtitle;
            _subtitleFileName = subtitleFileName;
            VideoFileName = videoFileName;
            _audioTrackNumber = audioTrackNumber;
            subtitleListView1.Fill(subtitle);
            if (subtitleListView1.Items.Count > 0)
            {
                subtitleListView1.Items[0].Selected = true;
            }

            labelOtherSubtitleFileName.Text = otherSubtitleFileName;
            subtitleListView2.Fill(otherSubtitle);

            subtitleListView1.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom;
            subtitleListView2.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom;
            buttonSetSyncPoint.Anchor = AnchorStyles.Left;
            buttonAutoSetSyncPoints.Anchor = AnchorStyles.Left;
            autoSetSyncProgress.Anchor = AnchorStyles.Left;
            similarityScoreLabel.Anchor = AnchorStyles.Left;
            minFuzzScore.Anchor = AnchorStyles.Left;
            buttonRemoveSyncPoint.Anchor = AnchorStyles.Left;
            labelNoOfSyncPoints.Anchor = AnchorStyles.Left;
            listBoxSyncPoints.Anchor = AnchorStyles.Left;
            labelAdjustFactor.Anchor = listBoxSyncPoints.Anchor;
            labelOtherSubtitleFileName.Visible = true;
            subtitleListView2.Visible = true;
            buttonFindTextOther.Visible = true;
            Width = subtitleListView2.Width * 2 + 250;
            MinimumSize = new Size(Width - 50, MinimumSize.Height);
        }

        private void RefreshSynchronizationPointsUi()
        {
            buttonApplySync.Enabled = _synchronizationPoints.Count > 0;
            labelNoOfSyncPoints.Text = string.Format(LanguageSettings.Current.PointSync.SyncPointsX, _synchronizationPoints.Count);

            listBoxSyncPoints.Items.Clear();

            for (var i = 0; i < subtitleListView1.Items.Count; i++)
            {
                if (_synchronizationPoints.ContainsKey(i))
                {
                    var p = new Paragraph { StartTime = { TotalMilliseconds = _synchronizationPoints[i].Time.TotalMilliseconds } };
                    p.EndTime.TotalMilliseconds = p.StartTime.TotalMilliseconds + _subtitle.Paragraphs[i].DurationTotalMilliseconds;
                    subtitleListView1.SetStartTimeAndDuration(i, p, _subtitle.GetParagraphOrDefault(i + 1), _subtitle.GetParagraphOrDefault(i - 1));

                    var item = new ListBoxSyncPoint { Index = i, Text = _subtitle.Paragraphs[i].Number + " - " + p.StartTime };
                    listBoxSyncPoints.Items.Add(item);
                    subtitleListView1.SetNumber(_subtitle.Paragraphs[i].Number, "* * * *");
                }
                else
                {
                    subtitleListView1.SetNumber(i, _subtitle.Paragraphs[i].Number.ToString(CultureInfo.InvariantCulture));
                    subtitleListView1.SetStartTimeAndDuration(i, _subtitle.Paragraphs[i], _subtitle.GetParagraphOrDefault(i + 1), _subtitle.GetParagraphOrDefault(i - 1));
                }
            }

            SetViewColors(_otherSubtitle, subtitleListView2, _synchronizationPoints.Values.Select(v => v.OtherIndex), GetSelectedParagraph(_subtitle, subtitleListView1));
            SetViewColors(_subtitle, subtitleListView1, _synchronizationPoints.Keys, GetSelectedParagraph(_otherSubtitle, subtitleListView2));
        }

        private void buttonSetSyncPoint_Click(object sender, EventArgs e)
        {
            if (subtitleListView2.Visible)
            {
                SetSyncPointViaOtherSubtitle();
            }
            else
            {
                if (subtitleListView1.SelectedItems.Count == 1 && _subtitle != null)
                {
                    using (var getTime = new SetSyncPoint())
                    {
                        var index = subtitleListView1.SelectedItems[0].Index;
                        getTime.Initialize(_subtitle, _subtitleFileName, index, VideoFileName, _audioTrackNumber);
                        if (getTime.ShowDialog(this) == DialogResult.OK)
                        {
                            _synchronizationPoints[index] = (getTime.SynchronizationPoint, SubtitleListView.InvalidIndex);

                            RefreshSynchronizationPointsUi();
                            VideoFileName = getTime.VideoFileName;
                        }

                        Activate();
                        VideoFileName = getTime.VideoFileName;
                    }
                }
            }

            SetSyncFactorLabel();
        }

        private void SetSyncPointViaOtherSubtitle()
        {
            if (subtitleListView1.SelectedItems.Count != 1 || subtitleListView2.SelectedItems.Count != 1)
            {
                return;
            }

            if (_otherSubtitle != null && subtitleListView2.SelectedItems.Count == 1)
            {
                var index = subtitleListView1.SelectedItems[0].Index;
                var indexOther = subtitleListView2.SelectedItems[0].Index;

                _synchronizationPoints[index] = (TimeSpan.FromMilliseconds(_otherSubtitle.Paragraphs[indexOther].StartTime.TotalMilliseconds), indexOther);

                RefreshSynchronizationPointsUi();
            }

            SetSyncFactorLabel();
        }

        private void buttonRemoveSyncPoint_Click(object sender, EventArgs e)
        {
            if (subtitleListView1.SelectedItems.Count == 1 && _subtitle != null)
            {
                var index = subtitleListView1.SelectedItems[0].Index;
                if (_synchronizationPoints.ContainsKey(index))
                {
                    _synchronizationPoints.Remove(index);
                }

                RefreshSynchronizationPointsUi();
            }

            SetSyncFactorLabel();
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            if (buttonApplySync.Enabled)
            {
                buttonSync_Click(null, null);
            }

            DialogResult = DialogResult.OK;
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        private void SyncPointsSync_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
            }
            else if (e.KeyData == UiUtil.HelpKeys)
            {
                UiUtil.ShowHelp("#sync");
                e.SuppressKeyPress = true;
            }
            else if (_mainGeneralGoToNextSubtitle == e.KeyData || _mainGeneralGoToNextSubtitlePlayTranslate == e.KeyData)
            {
                var selectedIndex = 0;
                if (subtitleListView1.SelectedItems.Count > 0)
                {
                    selectedIndex = subtitleListView1.SelectedItems[0].Index;
                    selectedIndex++;
                }

                subtitleListView1.SelectIndexAndEnsureVisible(selectedIndex);
                e.SuppressKeyPress = true;
            }
            else if (_mainGeneralGoToPrevSubtitle == e.KeyData || _mainGeneralGoToPrevSubtitlePlayTranslate == e.KeyData)
            {
                var selectedIndex = 0;
                if (subtitleListView1.SelectedItems.Count > 0)
                {
                    selectedIndex = subtitleListView1.SelectedItems[0].Index;
                    selectedIndex--;
                }

                subtitleListView1.SelectIndexAndEnsureVisible(selectedIndex);
                e.SuppressKeyPress = true;
            }
            else if (e.KeyData == (Keys.Control | Keys.G))
            {
                var subView = subtitleListView1;
                if (subtitleListView2 != null && subtitleListView2.Visible && !subtitleListView1.Focused)
                {
                    var x = PointToClient(MousePosition).X;
                    if (x >= subtitleListView2.Left && x <= subtitleListView2.Left + subtitleListView2.Width)
                    {
                        subView = subtitleListView2;
                    }
                }

                using (var gotoForm = new GoToLine())
                {
                    gotoForm.Initialize(1, subView.Items.Count);
                    if (gotoForm.ShowDialog() == DialogResult.OK)
                    {
                        subView.SelectIndexAndEnsureVisible(gotoForm.LineNumber - 1, true);
                    }
                }
            }
        }

        private void SetSyncFactorLabel()
        {
            labelAdjustFactor.Text = string.Empty;
            if (_synchronizationPoints.Count == 1)
            {
                var startPos = _synchronizationPoints.First().Value.Time.TotalMilliseconds / TimeCode.BaseUnit;
                var subStart = _originalSubtitle.Paragraphs[_synchronizationPoints.First().Key].StartTime.TotalMilliseconds / TimeCode.BaseUnit;

                var adjustment = startPos - subStart;
                labelAdjustFactor.Text = $"{adjustment:+0.000;-0.000}";
            }
            else if (_synchronizationPoints.Count == 2)
            {
                var startPos = _synchronizationPoints.First().Value.Time.TotalMilliseconds / TimeCode.BaseUnit;
                var endPos = _synchronizationPoints.Last().Value.Time.TotalMilliseconds / TimeCode.BaseUnit;

                var subStart = _originalSubtitle.Paragraphs[_synchronizationPoints.First().Key].StartTime.TotalMilliseconds / TimeCode.BaseUnit;
                var subEnd = _originalSubtitle.Paragraphs[_synchronizationPoints.Last().Key].StartTime.TotalMilliseconds / TimeCode.BaseUnit;

                var subDiff = subEnd - subStart;
                var realDiff = endPos - startPos;

                // speed factor
                var factor = realDiff / subDiff;

                // adjust to starting position
                var adjust = startPos - subStart * factor;

                labelAdjustFactor.Text = $"*{factor:0.000}, {adjust:+0.000;-0.000}";
            }
        }

        private void Sync(int startIndex, int endIndex, int minIndex, int maxIndex, double startPos, double endPos)
        {
            if (endPos > startPos)
            {
                var subStart = _originalSubtitle.Paragraphs[startIndex].StartTime.TotalMilliseconds / TimeCode.BaseUnit;
                var subEnd = _originalSubtitle.Paragraphs[endIndex].StartTime.TotalMilliseconds / TimeCode.BaseUnit;

                var subDiff = subEnd - subStart;
                var realDiff = endPos - startPos;

                // speed factor
                var factor = Math.Abs(subDiff) < 0.001 ? 1 : realDiff / subDiff;

                // adjust to starting position
                var adjust = startPos - subStart * factor;

                for (var i = minIndex; i < _subtitle.Paragraphs.Count && i <= maxIndex; i++)
                {
                    var p = _subtitle.Paragraphs[i];
                    p.StartTime.TotalMilliseconds = _originalSubtitle.Paragraphs[i].StartTime.TotalMilliseconds;
                    p.EndTime.TotalMilliseconds = _originalSubtitle.Paragraphs[i].EndTime.TotalMilliseconds;
                    p.Adjust(factor, adjust);
                }
            }
        }

        private void buttonSync_Click(object sender, EventArgs e)
        {
            if (_synchronizationPoints.Count == 1)
            {
                foreach (var kvp in _synchronizationPoints)
                {
                    AdjustViaShowEarlierLater(kvp.Key, kvp.Value.Time.TotalMilliseconds);
                }

                _synchronizationPoints = new SortedDictionary<int, (TimeSpan, int)>();
                subtitleListView1.Fill(_subtitle);
                RefreshSynchronizationPointsUi();
                return;
            }

            var endIndex = -1;
            var minIndex = 0;
            var syncIndices = new List<int>();
            foreach (var kvp in _synchronizationPoints)
            {
                syncIndices.Add(kvp.Key);
            }

            for (var i = 0; i < syncIndices.Count; i++)
            {
                if (i == 0)
                {
                    endIndex = syncIndices[i];
                }
                else
                {
                    var startIndex = endIndex;
                    endIndex = syncIndices[i];

                    int maxIndex;
                    if (i == syncIndices.Count - 1)
                    {
                        maxIndex = _subtitle.Paragraphs.Count;
                    }
                    else
                    {
                        maxIndex = syncIndices[i];
                    }

                    Sync(startIndex, endIndex, minIndex, maxIndex, _synchronizationPoints[startIndex].Time.TotalMilliseconds / TimeCode.BaseUnit, _synchronizationPoints[endIndex].Time.TotalMilliseconds / TimeCode.BaseUnit);

                    minIndex = endIndex;
                }
            }

            subtitleListView1.Fill(_subtitle);
            RefreshSynchronizationPointsUi();
        }

        private void AdjustViaShowEarlierLater(int index, double newTotalMilliseconds)
        {
            var oldTotalMilliseconds = _subtitle.Paragraphs[index].StartTime.TotalMilliseconds;
            var diff = newTotalMilliseconds - oldTotalMilliseconds;
            _subtitle.AddTimeToAllParagraphs(TimeSpan.FromMilliseconds(diff));
        }

        private void listBoxSyncPoints_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBoxSyncPoints.SelectedIndex >= 0)
            {
                var item = (ListBoxSyncPoint)listBoxSyncPoints.Items[listBoxSyncPoints.SelectedIndex];
                subtitleListView1.SelectIndexAndEnsureVisible(item.Index);
            }
        }

        private void SubtitleListView1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (subtitleListView1.SelectedItems.Count == 1)
            {
                var index = subtitleListView1.SelectedItems[0].Index;
                if (_synchronizationPoints.ContainsKey(index))
                {
                    buttonRemoveSyncPoint_Click(null, null);
                }
                else
                {
                    buttonSetSyncPoint_Click(null, null);
                }
            }
        }

        private void SyncPointsSyncResize(object sender, EventArgs e)
        {
            if (subtitleListView2.Visible)
            {
                var widthInMiddle = listBoxSyncPoints.Width;
                subtitleListView1.Width = (groupBoxImportResult.Width - widthInMiddle) / 2 - 12;
                subtitleListView2.Width = subtitleListView1.Width;
                subtitleListView2.Left = subtitleListView1.Left + subtitleListView1.Width + widthInMiddle + 10;
                listBoxSyncPoints.Left = subtitleListView1.Left + subtitleListView1.Width + 5;
                buttonSetSyncPoint.Left = listBoxSyncPoints.Left;
                autoSetSyncProgress.Left = listBoxSyncPoints.Left;
                buttonAutoSetSyncPoints.Left = listBoxSyncPoints.Left;
                similarityScoreLabel.Left = listBoxSyncPoints.Left;
                minFuzzScore.Left = listBoxSyncPoints.Left;
                buttonRemoveSyncPoint.Left = listBoxSyncPoints.Left;
                labelAdjustFactor.Left = listBoxSyncPoints.Left;
                labelNoOfSyncPoints.Left = listBoxSyncPoints.Left;
                labelOtherSubtitleFileName.Left = subtitleListView2.Left;
                buttonFindText.Left = subtitleListView1.Left + subtitleListView1.Width - buttonFindText.Width;
            }
        }

        private void SyncPointsSyncShown(object sender, EventArgs e)
        {
            SyncPointsSyncResize(null, null);
        }

        private void ButtonFindTextClick(object sender, EventArgs e)
        {
            using (var findSubtitle = new FindSubtitleLine())
            {
                findSubtitle.Initialize(_subtitle.Paragraphs, string.Empty);
                findSubtitle.ShowDialog();
                if (findSubtitle.SelectedIndex >= 0)
                {
                    subtitleListView1.SelectIndexAndEnsureVisible(findSubtitle.SelectedIndex);
                }
            }
        }

        private void ButtonFindTextOtherClick(object sender, EventArgs e)
        {
            using (var findSubtitle = new FindSubtitleLine())
            {
                findSubtitle.Initialize(_otherSubtitle.Paragraphs, string.Empty);
                findSubtitle.ShowDialog();
                if (findSubtitle.SelectedIndex >= 0)
                {
                    subtitleListView2.SelectIndexAndEnsureVisible(findSubtitle.SelectedIndex);
                }
            }
        }

        private void SubtitleListview1_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetViewColors(_otherSubtitle, subtitleListView2, _synchronizationPoints.Values.Select(v => v.OtherIndex), GetSelectedParagraph(_subtitle, subtitleListView1));
        }

        private void subtitleListView2_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetViewColors(_subtitle, subtitleListView1, _synchronizationPoints.Keys, GetSelectedParagraph(_otherSubtitle, subtitleListView2));
        }

        private static Paragraph GetSelectedParagraph(Subtitle subtitle, SubtitleListView view)
        {
            var selectedIndex = view.SelectedIndex;
            return selectedIndex == SubtitleListView.InvalidIndex ? null : subtitle.Paragraphs[selectedIndex];
        }


        private void SetViewColors(Subtitle subtitle, SubtitleListView view, IEnumerable<int> marked, Paragraph syncParagraph = null)
        {
            if (!view.Visible) return;
            var markedHashSet = marked.Where(v => v != SubtitleListView.InvalidIndex).ToHashSet();
            if (syncParagraph is null)
            {
                for (var i = 0; i < view.Items.Count; i++)
                {
                    view.SetBackgroundColor(i, CalculateBackgroundColor(view.BackColor, markedHashSet.Contains(i), 1));
                    view.SetForegroundColor(i, CalculateForegroundColor(view.ForeColor, view.GetBackgroundColor(i).Luminance(), 1));
                }
                return;
            }

            var selectedLocation = syncParagraph.StartTime.TotalMilliseconds;
            var selectedDuration = syncParagraph.DurationTotalMilliseconds;
            if (selectedDuration == 0)
                selectedDuration = 1000;


            (int Index, double Dinstance) closesIndex = (SubtitleListView.InvalidIndex, 0d);
            for (var i = 0; i < view.Items.Count; i++)
            {
                var distanceFromSelected = Math.Abs(subtitle.Paragraphs[i].StartTime.TotalMilliseconds - selectedLocation);
                var percentageDistance = distanceFromSelected / 5 / selectedDuration;
                view.SetBackgroundColor(i, CalculateBackgroundColor(view.BackColor, markedHashSet.Contains(i), percentageDistance));
                view.SetForegroundColor(i, CalculateForegroundColor(view.ForeColor, view.GetBackgroundColor(i).Luminance(), percentageDistance));
                if (closesIndex.Dinstance > distanceFromSelected || closesIndex.Index == SubtitleListView.InvalidIndex)
                {
                    closesIndex = (i, distanceFromSelected);
                }
            }

            if (closesIndex.Index == SubtitleListView.InvalidIndex)
                return;
            view.SetBackgroundColor(closesIndex.Index, CalculateBackgroundColor(view.BackColor, markedHashSet.Contains(closesIndex.Index), 0));
            view.SetForegroundColor(closesIndex.Index, CalculateForegroundColor(view.ForeColor, view.GetBackgroundColor(closesIndex.Index).Luminance(), 0));
            view.Items[closesIndex.Index].EnsureVisible();
        }
        private Color MarkedBackgroundColor { get; } = ColorTranslator.FromHtml("#6ebe6e");
        private Color VisualMarkBackgroundColor { get; } = Configuration.Settings.General.UseDarkTheme ? Color.LightGray : Color.DarkGray;

        private Color CalculateBackgroundColor(Color baseColor, bool shouldUseSyncColor, double percentageDistance)
        {
            var color = shouldUseSyncColor ? MarkedBackgroundColor : baseColor;
            if (percentageDistance >= 1) return color;

            var visualMarkedColorWithOffset = VisualMarkBackgroundColor.Blend(baseColor, percentageDistance);
            return shouldUseSyncColor
                ? visualMarkedColorWithOffset.Blend(MarkedBackgroundColor)
                : visualMarkedColorWithOffset;
        }

        private static Color CalculateForegroundColor(Color baseColor, double backGroundColorLuminance, double percentageDistance)
        {
            if (percentageDistance >= 1)
                return baseColor;
            if (backGroundColorLuminance < 0.5)
            {
                return Color.White;
            }

            return Color.Black;
        }

        private void buttonAutoSetSyncPoint2s_Click(object sender, EventArgs e)
        {
            double FUZZ_THRESHOLD = minFuzzScore.Value;

            HashSet<Anchor> anchors = new HashSet<Anchor>();

            for (int i = 0; i < _subtitle.Paragraphs.Count; i++)
            {
                var paragraph = _subtitle.Paragraphs[i];
                for (int j = 0; j < _otherSubtitle.Paragraphs.Count; j++)
                {
                    var otherParagraph = _otherSubtitle.Paragraphs[j];
                    double similarityScore = Fuzz.PartialTokenSetRatio(paragraph.Text, otherParagraph.Text);

                    if (similarityScore >= FUZZ_THRESHOLD)
                    {
                        anchors.Add(new Anchor(first: i, second: j));
                    }
                }
            }

            Console.WriteLine(anchors.Count);

            int mostCollisions;


            do
            {
                mostCollisions = 0;


                foreach (var anchor in anchors)
                {
                    anchor.Collisions = 0;
                }


                List<Anchor> currentAnchorsSnapshot = anchors.ToList();

                for (int i = 0; i < currentAnchorsSnapshot.Count - 1; i++)
                {
                    Anchor anchor = currentAnchorsSnapshot[i];
                    for (int j = i + 1; j < currentAnchorsSnapshot.Count; j++)
                    {
                        Anchor otherAnchor = currentAnchorsSnapshot[j];

                        bool isCrossing = (anchor.First > otherAnchor.First) != (anchor.Second > otherAnchor.Second);
                        bool hasSameFirstIndex = (anchor.First == otherAnchor.First);
                        bool hasSameSecondIndex = (anchor.Second == otherAnchor.Second);

                        if (isCrossing || hasSameFirstIndex || hasSameSecondIndex)
                        {
                            anchor.Collisions++;
                            otherAnchor.Collisions++;


                            if (anchor.Collisions > mostCollisions)
                            {
                                mostCollisions = anchor.Collisions;
                            }
                            if (otherAnchor.Collisions > mostCollisions)
                            {
                                mostCollisions = otherAnchor.Collisions;
                            }
                        }
                    }
                }

                if (mostCollisions > 0)
                {
                    anchors = anchors.Where(a => a.Collisions != mostCollisions).ToHashSet(); // this exists in C#!!! Finally microsoft doesn't make me want to kill myself
                }

            } while (mostCollisions > 0);

            _synchronizationPoints.Clear(); // idk if clearing is smart, can be removed later

            foreach (var anchor in anchors)
            {
                if (anchor.Second >= 0 && anchor.Second < _otherSubtitle.Paragraphs.Count)
                {
                    _synchronizationPoints[anchor.First] = (
                        TimeSpan.FromMilliseconds(_otherSubtitle.Paragraphs[anchor.Second].StartTime.TotalMilliseconds),
                        anchor.Second
                    );
                }
            }

            RefreshSynchronizationPointsUi();
        }

        private async void buttonAutoSetSyncPoints_Click(object sender, EventArgs e)
        {
            Console.WriteLine("Searching for sync points.");
            double FUZZ_THRESHOLD = minFuzzScore.Value;

            buttonAutoSetSyncPoints.Enabled = false;
            autoSetSyncProgress.Value = 0;
            autoSetSyncProgress.Visible = true;
            autoSetSyncProgress.Maximum = _subtitle.Paragraphs.Count;

            try
            {
                HashSet<Anchor> resultPoints = await Task.Run(() =>
                {
                    
                    ConcurrentBag<Anchor> t_anchors = new ConcurrentBag<Anchor>();

                    Parallel.For(0, _subtitle.Paragraphs.Count, i => {
                        var paragraph = _subtitle.Paragraphs[i];
                        for (int j = 0; j < _otherSubtitle.Paragraphs.Count; j++)
                        {
                            var otherParagraph = _otherSubtitle.Paragraphs[j];
                            double similarityScore = Fuzz.PartialTokenSetRatio(paragraph.Text, otherParagraph.Text);

                            if (similarityScore >= FUZZ_THRESHOLD)
                            {
                                t_anchors.Add(new Anchor(first: i, second: j));
                            }
                        }
                        this.Invoke((MethodInvoker)delegate {
                            autoSetSyncProgress.Value++;
                        });
                    });

                    this.Invoke((MethodInvoker)delegate {
                        autoSetSyncProgress.Value = 0;
                        autoSetSyncProgress.Maximum = 1;
                    });

                    HashSet<Anchor> anchors = new HashSet<Anchor>(t_anchors);

                    Console.WriteLine(anchors.Count);

                    int mostCollisions;


                    do
                    {
                        mostCollisions = 0;


                        foreach (var anchor in anchors)
                        {
                            anchor.Collisions = 0;
                        }


                        List<Anchor> currentAnchorsSnapshot = anchors.ToList();

                        Parallel.For(0, currentAnchorsSnapshot.Count - 1, i =>
                        {
                            Anchor anchor = currentAnchorsSnapshot[i];

                            for (int j = i + 1; j < currentAnchorsSnapshot.Count; j++)
                            {
                                Anchor otherAnchor = currentAnchorsSnapshot[j];

                                bool isCollision = (anchor.First > otherAnchor.First) != (anchor.Second > otherAnchor.Second) ||
                                                   (anchor.First == otherAnchor.First) ||
                                                   (anchor.Second == otherAnchor.Second);

                                if (isCollision)
                                {
                                    System.Threading.Interlocked.Increment(ref anchor.Collisions);
                                    System.Threading.Interlocked.Increment(ref otherAnchor.Collisions);
                                }
                            }
                        });

                        foreach (var anchor in currentAnchorsSnapshot)
                        {
                            if (anchor.Collisions > mostCollisions)
                            {
                                mostCollisions = anchor.Collisions;
                            }
                        }

                        if (mostCollisions > 0)
                        {
                            if (mostCollisions > 10000)
                            {
                                anchors = anchors.Where(
                                    a => a.Collisions < 0.85 * mostCollisions
                                ).ToHashSet(); // this exists in C#!!! Finally microsoft doesn't make me want to kill myself
                            }
                            else if (mostCollisions > 100)
                            {
                                anchors = anchors.Where(
                                    a => a.Collisions < 0.95 * mostCollisions
                                ).ToHashSet(); // this exists in C#!!! Finally microsoft doesn't make me want to kill myself
                            }
                            else
                            {
                                anchors = anchors.Where(a => a.Collisions != mostCollisions).ToHashSet(); // this exists in C#!!! Finally microsoft doesn't make me want to kill myself
                            }
                            
                        }
                        Console.WriteLine("Loop " + mostCollisions);

                        this.Invoke((MethodInvoker)delegate {
                            if (mostCollisions > autoSetSyncProgress.Maximum)
                            {
                                autoSetSyncProgress.Maximum = mostCollisions;
                            }
                            autoSetSyncProgress.Value = autoSetSyncProgress.Maximum - mostCollisions;
                        });
                    } while (mostCollisions > 0);

                    Console.WriteLine(anchors.Count);
                    return anchors;
                });
                _synchronizationPoints.Clear(); // idk if clearing is smart, can be removed later

                foreach (var anchor in resultPoints)
                {
                    if (anchor.Second >= 0 && anchor.Second < _otherSubtitle.Paragraphs.Count)
                    {
                        _synchronizationPoints[anchor.First] = (
                            TimeSpan.FromMilliseconds(_otherSubtitle.Paragraphs[anchor.Second].StartTime.TotalMilliseconds),
                            anchor.Second
                        );
                    }
                }

                RefreshSynchronizationPointsUi();
            }
            catch (Exception ex)
            {
                // Handle any exceptions that occurred in the background task
                MessageBox.Show($"An error occurred during synchronization: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Console.WriteLine(ex.ToString()); // Log the full exception for debugging
            }
            finally
            {
                buttonAutoSetSyncPoints.Enabled = true;
                autoSetSyncProgress.Visible = false;
            }
        }
    }
}
