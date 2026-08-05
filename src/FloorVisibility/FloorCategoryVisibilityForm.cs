using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.DB;

using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace REVIT_TAP
{
    public enum FloorVisibilityAction
    {
        Cancel,
        Apply,
        Restore
    }

    public class FloorCategoryVisibilityForm :
        WinForms.Form
    {
        private readonly
            IList<FloorVisibilityLevelOption>
                _levelOptions;

        private readonly
            IList<FloorVisibilityCategoryOption>
                _categoryOptions;

        private readonly
            FloorVisibilitySelectionSettings
                _savedSettings;

        private WinForms.CheckedListBox
            _levelCheckedListBox;

        private WinForms.CheckedListBox
            _categoryCheckedListBox;

        private WinForms.Button
            _selectAllLevelsButton;

        private WinForms.Button
            _clearAllLevelsButton;

        private WinForms.Button
            _selectAllCategoriesButton;

        private WinForms.Button
            _clearAllCategoriesButton;

        private WinForms.Button
            _applyButton;

        private WinForms.Button
            _restoreButton;

        private WinForms.Button
            _cancelButton;

        public FloorVisibilityAction SelectedAction
        {
            get;
            private set;
        }

        public FloorCategoryVisibilityForm(
            IList<FloorVisibilityLevelOption>
                levelOptions,
            IList<FloorVisibilityCategoryOption>
                categoryOptions,
            FloorVisibilitySelectionSettings
                savedSettings)
        {
            _levelOptions =
                levelOptions ??
                new List<FloorVisibilityLevelOption>();

            _categoryOptions =
                categoryOptions ??
                new List<FloorVisibilityCategoryOption>();

            _savedSettings =
                savedSettings ??
                new FloorVisibilitySelectionSettings();

            SelectedAction =
                FloorVisibilityAction.Cancel;

            InitializeForm();
            LoadOptions();
        }

        public ISet<int> GetSelectedLevelIds()
        {
            HashSet<int> result =
                new HashSet<int>();

            foreach (object item in
                _levelCheckedListBox.CheckedItems)
            {
                FloorVisibilityLevelOption option =
                    item as FloorVisibilityLevelOption;

                if (option == null ||
                    option.LevelId == null)
                {
                    continue;
                }

                result.Add(
                    option.LevelId.IntegerValue
                );
            }

            return result;
        }

        public ISet<int> GetSelectedCategoryIds()
        {
            HashSet<int> result =
                new HashSet<int>();

            foreach (object item in
                _categoryCheckedListBox.CheckedItems)
            {
                FloorVisibilityCategoryOption option =
                    item as
                        FloorVisibilityCategoryOption;

                if (option != null)
                {
                    result.Add(
                        option.CategoryId
                    );
                }
            }

            return result;
        }

        public IList<string> GetSelectedLevelNames()
        {
            return _levelCheckedListBox
                .CheckedItems
                .Cast<object>()
                .OfType<FloorVisibilityLevelOption>()
                .Select(
                    option =>
                        option.LevelName
                )
                .Where(
                    name =>
                        !string.IsNullOrWhiteSpace(
                            name
                        )
                )
                .Distinct(
                    StringComparer.OrdinalIgnoreCase
                )
                .ToList();
        }

        public IList<string>
            GetSelectedCategoryNames()
        {
            return _categoryCheckedListBox
                .CheckedItems
                .Cast<object>()
                .OfType<FloorVisibilityCategoryOption>()
                .Select(
                    option =>
                        option.CategoryName
                )
                .Where(
                    name =>
                        !string.IsNullOrWhiteSpace(
                            name
                        )
                )
                .Distinct(
                    StringComparer.OrdinalIgnoreCase
                )
                .ToList();
        }

        private void InitializeForm()
        {
            Text =
                "층별 부재 보기";

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
                52;

            descriptionLabel.Text =
                "보고 싶은 층과 표시할 카테고리를 " +
                "각각 여러 개 선택할 수 있습니다.\r\n" +
                "마지막으로 실행한 선택 상태를 저장하여 " +
                "다음 실행 때 그대로 표시합니다.";

            WinForms.Label levelLabel =
                new WinForms.Label();

            levelLabel.Left =
                18;

            levelLabel.Top =
                78;

            levelLabel.Width =
                400;

            levelLabel.Height =
                22;

            levelLabel.Text =
                "보고 싶은 층 · 중복 선택 가능";

            _selectAllLevelsButton =
                new WinForms.Button();

            _selectAllLevelsButton.Left =
                678;

            _selectAllLevelsButton.Top =
                70;

            _selectAllLevelsButton.Width =
                98;

            _selectAllLevelsButton.Height =
                30;

            _selectAllLevelsButton.Text =
                "층 전체 선택";

            _selectAllLevelsButton.Click +=
                SelectAllLevelsButton_Click;

            _clearAllLevelsButton =
                new WinForms.Button();

            _clearAllLevelsButton.Left =
                784;

            _clearAllLevelsButton.Top =
                70;

            _clearAllLevelsButton.Width =
                98;

            _clearAllLevelsButton.Height =
                30;

            _clearAllLevelsButton.Text =
                "층 선택 해제";

            _clearAllLevelsButton.Click +=
                ClearAllLevelsButton_Click;

            _levelCheckedListBox =
                new WinForms.CheckedListBox();

            _levelCheckedListBox.Left =
                18;

            _levelCheckedListBox.Top =
                106;

            _levelCheckedListBox.Width =
                864;

            _levelCheckedListBox.Height =
                170;

            _levelCheckedListBox.CheckOnClick =
                true;

            _levelCheckedListBox.IntegralHeight =
                false;

            _levelCheckedListBox.HorizontalScrollbar =
                true;

            WinForms.Label categoryLabel =
                new WinForms.Label();

            categoryLabel.Left =
                18;

            categoryLabel.Top =
                294;

            categoryLabel.Width =
                500;

            categoryLabel.Height =
                22;

            categoryLabel.Text =
                "표시할 카테고리 · 현재 문서 전체 · " +
                "중복 선택 가능";

            _selectAllCategoriesButton =
                new WinForms.Button();

            _selectAllCategoriesButton.Left =
                678;

            _selectAllCategoriesButton.Top =
                286;

            _selectAllCategoriesButton.Width =
                98;

            _selectAllCategoriesButton.Height =
                30;

            _selectAllCategoriesButton.Text =
                "전체 선택";

            _selectAllCategoriesButton.Click +=
                SelectAllCategoriesButton_Click;

            _clearAllCategoriesButton =
                new WinForms.Button();

            _clearAllCategoriesButton.Left =
                784;

            _clearAllCategoriesButton.Top =
                286;

            _clearAllCategoriesButton.Width =
                98;

            _clearAllCategoriesButton.Height =
                30;

            _clearAllCategoriesButton.Text =
                "선택 해제";

            _clearAllCategoriesButton.Click +=
                ClearAllCategoriesButton_Click;

            _categoryCheckedListBox =
                new WinForms.CheckedListBox();

            _categoryCheckedListBox.Left =
                18;

            _categoryCheckedListBox.Top =
                322;

            _categoryCheckedListBox.Width =
                864;

            _categoryCheckedListBox.Height =
                280;

            _categoryCheckedListBox.CheckOnClick =
                true;

            _categoryCheckedListBox.IntegralHeight =
                false;

            _categoryCheckedListBox.HorizontalScrollbar =
                true;

            WinForms.Label noticeLabel =
                new WinForms.Label();

            noticeLabel.Left =
                18;

            noticeLabel.Top =
                612;

            noticeLabel.Width =
                864;

            noticeLabel.Height =
                44;

            noticeLabel.ForeColor =
                Drawing.Color.DimGray;

            noticeLabel.Text =
                "※ 선택 기억 파일: " +
                FloorVisibilitySelectionSettings
                    .SettingsFilePath +
                "\r\n" +
                "※ 원복은 이 기능의 임시 숨기기/분리만 " +
                "해제하며 기존 영구 숨김 설정은 " +
                "변경하지 않습니다.";

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
                "선택 항목 보기";

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
                levelLabel
            );

            Controls.Add(
                _selectAllLevelsButton
            );

            Controls.Add(
                _clearAllLevelsButton
            );

            Controls.Add(
                _levelCheckedListBox
            );

            Controls.Add(
                categoryLabel
            );

            Controls.Add(
                _selectAllCategoriesButton
            );

            Controls.Add(
                _clearAllCategoriesButton
            );

            Controls.Add(
                _categoryCheckedListBox
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

        private void LoadOptions()
        {
            HashSet<string> savedLevelNames =
                new HashSet<string>(
                    _savedSettings
                        .SelectedLevelNames ??
                    new List<string>(),
                    StringComparer
                        .OrdinalIgnoreCase
                );

            bool matchedSavedLevel =
                false;

            foreach (
                FloorVisibilityLevelOption option
                in _levelOptions)
            {
                int itemIndex =
                    _levelCheckedListBox
                        .Items.Add(
                            option
                        );

                bool shouldCheck =
                    savedLevelNames.Count > 0 &&
                    savedLevelNames.Contains(
                        option.LevelName ??
                        string.Empty
                    );

                if (shouldCheck)
                {
                    _levelCheckedListBox
                        .SetItemChecked(
                            itemIndex,
                            true
                        );

                    matchedSavedLevel =
                        true;
                }
            }

            if (!matchedSavedLevel &&
                _levelCheckedListBox
                    .Items.Count > 0)
            {
                _levelCheckedListBox
                    .SetItemChecked(
                        0,
                        true
                    );
            }

            HashSet<string>
                savedCategoryNames =
                    new HashSet<string>(
                        _savedSettings
                            .SelectedCategoryNames ??
                        new List<string>(),
                        StringComparer
                            .OrdinalIgnoreCase
                    );

            HashSet<int> defaultCategoryIds =
                new HashSet<int>
                {
                    (int)BuiltInCategory
                        .OST_Floors,

                    (int)BuiltInCategory
                        .OST_StructuralFraming,

                    (int)BuiltInCategory
                        .OST_Walls
                };

            bool matchedSavedCategory =
                false;

            foreach (
                FloorVisibilityCategoryOption option
                in _categoryOptions)
            {
                int itemIndex =
                    _categoryCheckedListBox
                        .Items.Add(
                            option
                        );

                bool shouldCheckSaved =
                    savedCategoryNames.Count > 0 &&
                    savedCategoryNames.Contains(
                        option.CategoryName ??
                        string.Empty
                    );

                if (shouldCheckSaved)
                {
                    _categoryCheckedListBox
                        .SetItemChecked(
                            itemIndex,
                            true
                        );

                    matchedSavedCategory =
                        true;
                }
            }

            if (!matchedSavedCategory)
            {
                for (int index = 0;
                    index <
                    _categoryCheckedListBox
                        .Items.Count;
                    index++)
                {
                    FloorVisibilityCategoryOption option =
                        _categoryCheckedListBox
                            .Items[index]
                        as FloorVisibilityCategoryOption;

                    if (option == null)
                    {
                        continue;
                    }

                    if (defaultCategoryIds.Contains(
                        option.CategoryId))
                    {
                        _categoryCheckedListBox
                            .SetItemChecked(
                                index,
                                true
                            );
                    }
                }
            }
        }

        private void SelectAllLevelsButton_Click(
            object sender,
            EventArgs e)
        {
            SetAllChecked(
                _levelCheckedListBox,
                true
            );
        }

        private void ClearAllLevelsButton_Click(
            object sender,
            EventArgs e)
        {
            SetAllChecked(
                _levelCheckedListBox,
                false
            );
        }

        private void
            SelectAllCategoriesButton_Click(
                object sender,
                EventArgs e)
        {
            SetAllChecked(
                _categoryCheckedListBox,
                true
            );
        }

        private void
            ClearAllCategoriesButton_Click(
                object sender,
                EventArgs e)
        {
            SetAllChecked(
                _categoryCheckedListBox,
                false
            );
        }

        private static void SetAllChecked(
            WinForms.CheckedListBox listBox,
            bool isChecked)
        {
            if (listBox == null)
            {
                return;
            }

            for (int index = 0;
                index < listBox.Items.Count;
                index++)
            {
                listBox.SetItemChecked(
                    index,
                    isChecked
                );
            }
        }

        private void ApplyButton_Click(
            object sender,
            EventArgs e)
        {
            if (_levelCheckedListBox
                .CheckedItems.Count == 0)
            {
                WinForms.MessageBox.Show(
                    this,
                    "한 개 이상의 층을 " +
                    "선택해 주십시오.",
                    "층별 부재 보기",
                    WinForms.MessageBoxButtons.OK,
                    WinForms.MessageBoxIcon
                        .Information
                );

                return;
            }

            if (_categoryCheckedListBox
                .CheckedItems.Count == 0)
            {
                WinForms.MessageBox.Show(
                    this,
                    "한 개 이상의 카테고리를 " +
                    "선택해 주십시오.",
                    "층별 부재 보기",
                    WinForms.MessageBoxButtons.OK,
                    WinForms.MessageBoxIcon
                        .Information
                );

                return;
            }

            SelectedAction =
                FloorVisibilityAction.Apply;

            DialogResult =
                WinForms.DialogResult.OK;

            Close();
        }

        private void RestoreButton_Click(
            object sender,
            EventArgs e)
        {
            SelectedAction =
                FloorVisibilityAction.Restore;

            DialogResult =
                WinForms.DialogResult.OK;

            Close();
        }

        private void CancelButton_Click(
            object sender,
            EventArgs e)
        {
            SelectedAction =
                FloorVisibilityAction.Cancel;

            DialogResult =
                WinForms.DialogResult.Cancel;

            Close();
        }
    }
}

// =========================================================
// 코드 제목: 이전 선택을 기억하는 층별·카테고리 선택 창
// 파일명: FloorCategoryVisibilityForm.cs
// =========================================================
