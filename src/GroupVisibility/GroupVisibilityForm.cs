using System;
using System.Collections.Generic;
using System.Linq;

using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace REVIT_TAP
{
    public enum GroupVisibilityAction
    {
        Cancel,
        Apply,
        Restore
    }

    public class GroupVisibilityForm :
        WinForms.Form
    {
        private readonly
            IList<GroupVisibilityOption>
                _groupOptions;

        private readonly
            HashSet<int>
                _selectedGroupTypeIds;

        private WinForms.TextBox
            _filterTextBox;

        private WinForms.Button
            _clearFilterButton;

        private WinForms.Label
            _resultCountLabel;

        private WinForms.CheckedListBox
            _groupCheckedListBox;

        private WinForms.Button
            _selectAllButton;

        private WinForms.Button
            _clearAllButton;

        private WinForms.Button
            _applyButton;

        private WinForms.Button
            _restoreButton;

        private WinForms.Button
            _cancelButton;

        private bool
            _isRebuildingList;

        public GroupVisibilityAction SelectedAction
        {
            get;
            private set;
        }

        public GroupVisibilityForm(
            IList<GroupVisibilityOption>
                groupOptions)
        {
            _groupOptions =
                groupOptions ??
                new List<GroupVisibilityOption>();

            _selectedGroupTypeIds =
                new HashSet<int>();

            _isRebuildingList =
                false;

            SelectedAction =
                GroupVisibilityAction.Cancel;

            InitializeForm();
            RebuildFilteredList();
        }

        public ISet<int>
            GetSelectedGroupTypeIds()
        {
            CaptureVisibleSelection();

            return new HashSet<int>(
                _selectedGroupTypeIds
            );
        }

        private void InitializeForm()
        {
            Text =
                "그룹별 보기";

            StartPosition =
                WinForms.FormStartPosition
                    .CenterScreen;

            FormBorderStyle =
                WinForms.FormBorderStyle
                    .FixedDialog;

            MaximizeBox =
                false;

            MinimizeBox =
                false;

            ShowInTaskbar =
                false;

            ClientSize =
                new Drawing.Size(
                    900,
                    730
                );

            AutoScaleMode =
                WinForms.AutoScaleMode.Font;

            WinForms.Label descriptionLabel =
                new WinForms.Label();

            descriptionLabel.Left =
                18;

            descriptionLabel.Top =
                15;

            descriptionLabel.Width =
                864;

            descriptionLabel.Height =
                54;

            descriptionLabel.Text =
                "현재 모델의 모델 그룹 유형을 여러 개 " +
                "선택할 수 있습니다.\r\n" +
                "검색어를 입력하면 계단실-1, 계단실-2처럼 " +
                "이름이 비슷한 그룹을 빠르게 찾을 수 있습니다.";

            WinForms.Label filterLabel =
                new WinForms.Label();

            filterLabel.Left =
                18;

            filterLabel.Top =
                80;

            filterLabel.Width =
                88;

            filterLabel.Height =
                24;

            filterLabel.Text =
                "그룹 검색";

            _filterTextBox =
                new WinForms.TextBox();

            _filterTextBox.Left =
                108;

            _filterTextBox.Top =
                74;

            _filterTextBox.Width =
                566;

            _filterTextBox.Height =
                28;

            _filterTextBox.TextChanged +=
                FilterTextBox_TextChanged;

            _clearFilterButton =
                new WinForms.Button();

            _clearFilterButton.Left =
                682;

            _clearFilterButton.Top =
                72;

            _clearFilterButton.Width =
                92;

            _clearFilterButton.Height =
                30;

            _clearFilterButton.Text =
                "검색 지움";

            _clearFilterButton.Click +=
                ClearFilterButton_Click;

            _resultCountLabel =
                new WinForms.Label();

            _resultCountLabel.Left =
                782;

            _resultCountLabel.Top =
                70;

            _resultCountLabel.Width =
                100;

            _resultCountLabel.Height =
                36;

            _resultCountLabel.TextAlign =
                Drawing.ContentAlignment
                    .MiddleRight;

            WinForms.Label groupLabel =
                new WinForms.Label();

            groupLabel.Left =
                18;

            groupLabel.Top =
                118;

            groupLabel.Width =
                450;

            groupLabel.Height =
                22;

            groupLabel.Text =
                "표시할 모델 그룹 · 중복 선택 가능";

            _selectAllButton =
                new WinForms.Button();

            _selectAllButton.Left =
                604;

            _selectAllButton.Top =
                110;

            _selectAllButton.Width =
                132;

            _selectAllButton.Height =
                30;

            _selectAllButton.Text =
                "검색 결과 전체 선택";

            _selectAllButton.Click +=
                SelectAllButton_Click;

            _clearAllButton =
                new WinForms.Button();

            _clearAllButton.Left =
                744;

            _clearAllButton.Top =
                110;

            _clearAllButton.Width =
                138;

            _clearAllButton.Height =
                30;

            _clearAllButton.Text =
                "전체 선택 해제";

            _clearAllButton.Click +=
                ClearAllButton_Click;

            _groupCheckedListBox =
                new WinForms.CheckedListBox();

            _groupCheckedListBox.Left =
                18;

            _groupCheckedListBox.Top =
                148;

            _groupCheckedListBox.Width =
                864;

            _groupCheckedListBox.Height =
                436;

            _groupCheckedListBox.CheckOnClick =
                true;

            _groupCheckedListBox.IntegralHeight =
                false;

            _groupCheckedListBox.HorizontalScrollbar =
                true;

            _groupCheckedListBox.ItemCheck +=
                GroupCheckedListBox_ItemCheck;

            WinForms.Label noticeLabel =
                new WinForms.Label();

            noticeLabel.Left =
                18;

            noticeLabel.Top =
                594;

            noticeLabel.Width =
                864;

            noticeLabel.Height =
                48;

            noticeLabel.ForeColor =
                Drawing.Color.DimGray;

            noticeLabel.Text =
                "※ 검색은 그룹 유형명과 층 요약을 대상으로 하며, " +
                "띄어쓰기로 여러 검색어를 입력하면 " +
                "모두 포함된 항목만 표시합니다.\r\n" +
                "※ 검색 결과가 바뀌어도 이미 체크한 그룹 선택은 " +
                "유지됩니다.";

            _restoreButton =
                new WinForms.Button();

            _restoreButton.Left =
                18;

            _restoreButton.Top =
                674;

            _restoreButton.Width =
                210;

            _restoreButton.Height =
                38;

            _restoreButton.Text =
                "전체 보이기 · 원복";

            _restoreButton.Click +=
                RestoreButton_Click;

            _cancelButton =
                new WinForms.Button();

            _cancelButton.Left =
                648;

            _cancelButton.Top =
                674;

            _cancelButton.Width =
                110;

            _cancelButton.Height =
                38;

            _cancelButton.Text =
                "취소";

            _cancelButton.Click +=
                CancelButton_Click;

            _applyButton =
                new WinForms.Button();

            _applyButton.Left =
                766;

            _applyButton.Top =
                674;

            _applyButton.Width =
                116;

            _applyButton.Height =
                38;

            _applyButton.Text =
                "선택 그룹 보기";

            _applyButton.Font =
                new Drawing.Font(
                    _applyButton.Font,
                    Drawing.FontStyle.Bold
                );

            _applyButton.Click +=
                ApplyButton_Click;

            AcceptButton =
                _applyButton;

            CancelButton =
                _cancelButton;

            Controls.Add(
                descriptionLabel
            );

            Controls.Add(
                filterLabel
            );

            Controls.Add(
                _filterTextBox
            );

            Controls.Add(
                _clearFilterButton
            );

            Controls.Add(
                _resultCountLabel
            );

            Controls.Add(
                groupLabel
            );

            Controls.Add(
                _selectAllButton
            );

            Controls.Add(
                _clearAllButton
            );

            Controls.Add(
                _groupCheckedListBox
            );

            Controls.Add(
                noticeLabel
            );

            Controls.Add(
                _restoreButton
            );

            Controls.Add(
                _cancelButton
            );

            Controls.Add(
                _applyButton
            );
        }

        private void FilterTextBox_TextChanged(
            object sender,
            EventArgs e)
        {
            CaptureVisibleSelection();
            RebuildFilteredList();
        }

        private void ClearFilterButton_Click(
            object sender,
            EventArgs e)
        {
            _filterTextBox.Text =
                string.Empty;

            _filterTextBox.Focus();
        }

        private void
            GroupCheckedListBox_ItemCheck(
                object sender,
                WinForms.ItemCheckEventArgs e)
        {
            if (_isRebuildingList ||
                e.Index < 0 ||
                e.Index >=
                    _groupCheckedListBox
                        .Items.Count)
            {
                return;
            }

            GroupVisibilityOption option =
                _groupCheckedListBox
                    .Items[e.Index]
                as GroupVisibilityOption;

            if (option == null ||
                option.GroupTypeId == null)
            {
                return;
            }

            int typeId =
                option.GroupTypeId
                    .IntegerValue;

            if (e.NewValue ==
                WinForms.CheckState.Checked)
            {
                _selectedGroupTypeIds.Add(
                    typeId
                );
            }
            else
            {
                _selectedGroupTypeIds.Remove(
                    typeId
                );
            }

            UpdateResultCountLabel();
        }

        private void RebuildFilteredList()
        {
            string filterText =
                (_filterTextBox == null
                    ? string.Empty
                    : _filterTextBox.Text)
                .Trim();

            IList<GroupVisibilityOption>
                filteredOptions =
                    _groupOptions
                        .Where(
                            option =>
                                IsFilterMatch(
                                    option,
                                    filterText
                                )
                        )
                        .ToList();

            _isRebuildingList =
                true;

            _groupCheckedListBox.BeginUpdate();

            try
            {
                _groupCheckedListBox
                    .Items.Clear();

                foreach (
                    GroupVisibilityOption option
                    in filteredOptions)
                {
                    bool isChecked =
                        option.GroupTypeId != null &&
                        _selectedGroupTypeIds.Contains(
                            option.GroupTypeId
                                .IntegerValue
                        );

                    _groupCheckedListBox
                        .Items.Add(
                            option,
                            isChecked
                        );
                }
            }
            finally
            {
                _groupCheckedListBox.EndUpdate();

                _isRebuildingList =
                    false;
            }

            UpdateResultCountLabel();
        }

        private static bool IsFilterMatch(
            GroupVisibilityOption option,
            string filterText)
        {
            if (option == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(
                filterText
            ))
            {
                return true;
            }

            string searchableText =
                (
                    (option.GroupTypeName ??
                        string.Empty) +
                    " " +
                    (option.LevelSummary ??
                        string.Empty)
                )
                .ToUpperInvariant();

            string[] tokens =
                filterText.Split(
                    new[]
                    {
                        ' ',
                        '\t',
                        ',',
                        ';'
                    },
                    StringSplitOptions
                        .RemoveEmptyEntries
                );

            foreach (string token in tokens)
            {
                string normalizedToken =
                    token
                        .Trim()
                        .ToUpperInvariant();

                if (!searchableText.Contains(
                    normalizedToken
                ))
                {
                    return false;
                }
            }

            return true;
        }

        private void CaptureVisibleSelection()
        {
            if (_groupCheckedListBox == null)
            {
                return;
            }

            for (int index = 0;
                index <
                    _groupCheckedListBox
                        .Items.Count;
                index++)
            {
                GroupVisibilityOption option =
                    _groupCheckedListBox
                        .Items[index]
                    as GroupVisibilityOption;

                if (option == null ||
                    option.GroupTypeId == null)
                {
                    continue;
                }

                int typeId =
                    option.GroupTypeId
                        .IntegerValue;

                if (_groupCheckedListBox
                    .GetItemChecked(index))
                {
                    _selectedGroupTypeIds.Add(
                        typeId
                    );
                }
                else
                {
                    _selectedGroupTypeIds.Remove(
                        typeId
                    );
                }
            }
        }

        private void SelectAllButton_Click(
            object sender,
            EventArgs e)
        {
            _isRebuildingList =
                true;

            try
            {
                for (int index = 0;
                    index <
                        _groupCheckedListBox
                            .Items.Count;
                    index++)
                {
                    GroupVisibilityOption option =
                        _groupCheckedListBox
                            .Items[index]
                        as GroupVisibilityOption;

                    if (option == null ||
                        option.GroupTypeId == null)
                    {
                        continue;
                    }

                    _selectedGroupTypeIds.Add(
                        option.GroupTypeId
                            .IntegerValue
                    );

                    _groupCheckedListBox
                        .SetItemChecked(
                            index,
                            true
                        );
                }
            }
            finally
            {
                _isRebuildingList =
                    false;
            }

            UpdateResultCountLabel();
        }

        private void ClearAllButton_Click(
            object sender,
            EventArgs e)
        {
            _selectedGroupTypeIds.Clear();

            _isRebuildingList =
                true;

            try
            {
                for (int index = 0;
                    index <
                        _groupCheckedListBox
                            .Items.Count;
                    index++)
                {
                    _groupCheckedListBox
                        .SetItemChecked(
                            index,
                            false
                        );
                }
            }
            finally
            {
                _isRebuildingList =
                    false;
            }

            UpdateResultCountLabel();
        }

        private void UpdateResultCountLabel()
        {
            if (_resultCountLabel == null ||
                _groupCheckedListBox == null)
            {
                return;
            }

            _resultCountLabel.Text =
                _groupCheckedListBox
                    .Items.Count +
                "/" +
                _groupOptions.Count +
                "\r\n선택 " +
                _selectedGroupTypeIds.Count;
        }

        private void ApplyButton_Click(
            object sender,
            EventArgs e)
        {
            CaptureVisibleSelection();

            if (_selectedGroupTypeIds.Count == 0)
            {
                WinForms.MessageBox.Show(
                    this,
                    "한 개 이상의 모델 그룹을 " +
                    "선택해 주십시오.",
                    "그룹별 보기",
                    WinForms.MessageBoxButtons.OK,
                    WinForms.MessageBoxIcon
                        .Information
                );

                return;
            }

            SelectedAction =
                GroupVisibilityAction.Apply;

            DialogResult =
                WinForms.DialogResult.OK;

            Close();
        }

        private void RestoreButton_Click(
            object sender,
            EventArgs e)
        {
            SelectedAction =
                GroupVisibilityAction.Restore;

            DialogResult =
                WinForms.DialogResult.OK;

            Close();
        }

        private void CancelButton_Click(
            object sender,
            EventArgs e)
        {
            SelectedAction =
                GroupVisibilityAction.Cancel;

            DialogResult =
                WinForms.DialogResult.Cancel;

            Close();
        }
    }
}

// =========================================================
// 코드 제목: 검색 필터가 포함된 현재 모델 그룹 복수 선택 창
// 파일명: GroupVisibilityForm.cs
// =========================================================
