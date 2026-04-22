using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace WpfApp_4
{
    public partial class MainWindow : Window
    {
        private string currentFilePath = null;
        private bool isTextChanged = false;
        private ObservableCollection<SearchMatch> searchMatches;
        private int lastHighlightStart = -1;
        private int lastHighlightLength = 0;

        // Регулярные выражения
        private static readonly Regex PassportRegex = new Regex(
            @"\b(?!00)\d{4}[ -](?!000000)\d{6}\b",
            RegexOptions.Compiled
        );

        private static readonly Regex InitialsRegex = new Regex(
            @"\b[А-ЯЁ]\.([А-ЯЁ]\.)\s[А-ЯЁ][а-яё]+(-[А-ЯЁ][а-яё]+)?\b",
            RegexOptions.Compiled
        );

        private static readonly Regex DateRegexBasic = new Regex(
            @"\b(0[1-9]|1[0-2])/(0[1-9]|[12][0-9]|3[01])/(19|20)\d{2}\b",
            RegexOptions.Compiled
        );

        public MainWindow()
        {
            InitializeComponent();
            searchMatches = new ObservableCollection<SearchMatch>();
            ResultsDataGrid.ItemsSource = searchMatches;
            UpdateStatus("Готов", 0);
        }

        // ФАЙЛ
        private void New_Click(object sender, RoutedEventArgs e)
        {
            if (CheckSaveChanges())
            {
                EditorTextBox.Clear();
                ClearResults();
                currentFilePath = null;
                isTextChanged = false;
                UpdateTitle();
                UpdateStatus("Создан новый файл", 0);
            }
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            if (CheckSaveChanges())
            {
                OpenFileDialog dlg = new OpenFileDialog();
                dlg.Filter = "Текстовые файлы (*.txt)|*.txt|Java файлы (*.java)|*.java|Все файлы (*.*)|*.*";

                if (dlg.ShowDialog() == true)
                {
                    try
                    {
                        EditorTextBox.Text = File.ReadAllText(dlg.FileName);
                        currentFilePath = dlg.FileName;
                        isTextChanged = false;
                        UpdateTitle();
                        UpdateStatus($"Открыт: {dlg.FileName}", 0);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при открытии файла: {ex.Message}", "Ошибка",
                                      MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentFilePath))
            {
                SaveAs_Click(sender, e);
            }
            else
            {
                try
                {
                    File.WriteAllText(currentFilePath, EditorTextBox.Text);
                    isTextChanged = false;
                    UpdateTitle();
                    UpdateStatus($"Сохранено: {currentFilePath}", searchMatches.Count);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "Текстовые файлы (*.txt)|*.txt|Java файлы (*.java)|*.java|Все файлы (*.*)|*.*";

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(dlg.FileName, EditorTextBox.Text);
                    currentFilePath = dlg.FileName;
                    isTextChanged = false;
                    UpdateTitle();
                    UpdateStatus($"Сохранено как: {dlg.FileName}", searchMatches.Count);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            if (CheckSaveChanges())
            {
                Application.Current.Shutdown();
            }
        }

        // ПРАВКА
        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            if (EditorTextBox.CanUndo)
            {
                EditorTextBox.Undo();
                UpdateStatus("Отмена действия", searchMatches.Count);
            }
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            if (EditorTextBox.CanRedo)
            {
                EditorTextBox.Redo();
                UpdateStatus("Возврат действия", searchMatches.Count);
            }
        }

        private void Cut_Click(object sender, RoutedEventArgs e)
        {
            EditorTextBox.Cut();
            UpdateStatus("Вырезано", searchMatches.Count);
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            EditorTextBox.Copy();
            UpdateStatus("Скопировано", searchMatches.Count);
        }

        private void Paste_Click(object sender, RoutedEventArgs e)
        {
            EditorTextBox.Paste();
            UpdateStatus("Вставлено", searchMatches.Count);
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            EditorTextBox.SelectedText = "";
            UpdateStatus("Удалено", searchMatches.Count);
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            EditorTextBox.SelectAll();
            UpdateStatus("Выделено всё", searchMatches.Count);
        }

        // ПОИСК 
        private void RunSearch_Click(object sender, RoutedEventArgs e)
        {
            PerformSearch();
        }

        private void ClearResults_Click(object sender, RoutedEventArgs e)
        {
            ClearResults();
            UpdateStatus("Результаты очищены", 0);
        }

        private void ClearHighlights_Click(object sender, RoutedEventArgs e)
        {
            ClearHighlight();
            UpdateStatus("Подсветка очищена", searchMatches.Count);
        }

        private void PerformSearch()
        {
            ClearHighlight();

            string text = EditorTextBox.Text;
            if (string.IsNullOrEmpty(text))
            {
                UpdateStatus("Нет текста для поиска", 0);
                return;
            }

            int selectedIndex = SearchTypeCombo.SelectedIndex;
            Regex regex = null;
            string typeName = "";

            switch (selectedIndex)
            {
                case 0:
                    regex = PassportRegex;
                    typeName = "паспортов";
                    break;
                case 1:
                    regex = InitialsRegex;
                    typeName = "инициалов и фамилий";
                    break;
                case 2:
                    regex = DateRegexBasic;
                    typeName = "дат";
                    break;
            }

            searchMatches.Clear();
            var matches = regex.Matches(text);

            foreach (Match match in matches)
            {
                string matchedText = match.Value;

                // Дополнительная проверка для дат (високосные годы)
                if (selectedIndex == 2 && !IsValidDate(matchedText))
                {
                    continue;
                }

                int line = GetLineNumber(text, match.Index);
                int column = GetColumnNumber(text, match.Index);
                searchMatches.Add(new SearchMatch
                {
                    Text = matchedText,
                    Line = line,
                    Column = column,
                    Length = match.Length,
                    StartIndex = match.Index
                });
            }

            UpdateStatus($"Поиск {typeName} выполнен", searchMatches.Count);

            if (searchMatches.Count > 0)
            {
                ResultsDataGrid.SelectedIndex = 0;
            }
        }

        private bool IsValidDate(string dateStr)
        {
            try
            {
                string[] parts = dateStr.Split('/');
                int month = int.Parse(parts[0]);
                int day = int.Parse(parts[1]);
                int year = int.Parse(parts[2]);

                if (month < 1 || month > 12) return false;

                int[] daysInMonth = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

                // Високосный год
                if (month == 2)
                {
                    bool isLeap = (year % 4 == 0 && year % 100 != 0) || (year % 400 == 0);
                    if (isLeap) daysInMonth[1] = 29;
                }

                return day >= 1 && day <= daysInMonth[month - 1];
            }
            catch
            {
                return false;
            }
        }

        private int GetLineNumber(string text, int position)
        {
            if (position < 0) return 1;

            return text.Substring(0, position).Split('\n').Length;
        }

        private int GetColumnNumber(string text, int position)
        {
            if (position < 0) return 1;
            int lastNewLine = text.LastIndexOf('\n', position - 1);
            if (lastNewLine == -1) return position + 1;
            return position - lastNewLine;
        }

        // ПОДСВЕТКА И НАВИГАЦИЯ 
        private void ResultsDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ResultsDataGrid.SelectedItem is SearchMatch selectedMatch)
            {
                HighlightAndGoToMatch(selectedMatch);
            }
        }

        private void HighlightAndGoToMatch(SearchMatch match)
        {
            try
            {
                EditorTextBox.Focus();
                EditorTextBox.Select(match.StartIndex, match.Length);
                EditorTextBox.ScrollToLine(match.Line - 1);

                lastHighlightStart = match.StartIndex;
                lastHighlightLength = match.Length;

                EditorTextBox.SelectionBrush = Brushes.Yellow;
                EditorTextBox.SelectionOpacity = 0.7;

                UpdateStatus($"Выделен фрагмент: \"{match.Text}\" (строка {match.Line}, столбец {match.Column})", searchMatches.Count);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Ошибка при выделении: {ex.Message}", searchMatches.Count);
            }
        }

        private void ClearHighlight()
        {
            if (lastHighlightStart >= 0)
            {
                EditorTextBox.Select(lastHighlightStart, 0);
                lastHighlightStart = -1;
                lastHighlightLength = 0;
            }
        }

        private void ClearResults()
        {
            searchMatches.Clear();
            ClearHighlight();
        }

        // СПРАВКА 
        private void Help_Click(object sender, RoutedEventArgs e)
        {
            string helpText = "СИНТАКСИЧЕСКИЙ АНАЛИЗАТОР - Объявление строковых констант Java\n\n" +
                            "ОСНОВНЫЕ ФУНКЦИИ:\n" +
                            "- Введите Java код в область редактирования\n" +
                            "- Нажмите кнопку 'Найти' или F5 для поиска\n" +
                            "- Результаты поиска отобразятся в таблице\n\n" +
                            "ТИПЫ ПОИСКА:\n\n" +
                            "1. Паспорт РФ\n" +
                            "   Формат: серия (4 цифры, не 00) + пробел/дефис + номер (6 цифр, не 000000)\n" +
                            "   Пример: 4512 345678, 1203-901234\n\n" +
                            "2. Инициалы и фамилия\n" +
                            "   Формат: И.О. Фамилия (с заглавной, возможен дефис)\n" +
                            "   Пример: И.И. Иванов, С.А. Петров\n\n" +
                            "3. Дата MM/DD/YYYY\n" +
                            "   С учётом високосных годов (29 февраля только в високосные годы)\n" +
                            "   Пример: 02/29/2020, 12/31/1999\n\n" +
                            "НАВИГАЦИЯ:\n" +
                            "- Двойной клик на строке в таблице - переход к месту и подсветка";

            MessageBox.Show(helpText, "Справка", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            string aboutText = "Поиск подстрок с использованием регулярных выражений\n" +
                             "Лабораторная работа\n\n" +
                             "Автор: Дарчук Софья\n" +
                             "Группа: АП-326\n\n" +
                             "Вариант: №22, 14, 6\n" +
                             "- Паспорт РФ\n" +
                             "- Инициалы и фамилия\n" +
                             "- Дата MM/DD/YYYY с учётом високосных годов\n\n" +
                             "Метод поиска: Регулярные выражения";

            MessageBox.Show(aboutText, "О программе", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ 
        private void EditorTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            isTextChanged = true;
            UpdateTitle();
            UpdateCursorPosition();
            ClearResults();
        }

        private void EditorTextBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            UpdateCursorPosition();
        }

        private void UpdateCursorPosition()
        {
            int line = EditorTextBox.GetLineIndexFromCharacterIndex(EditorTextBox.CaretIndex);
            int col = EditorTextBox.CaretIndex - EditorTextBox.GetCharacterIndexFromLineIndex(line);
            int totalBytes = System.Text.Encoding.UTF8.GetByteCount(EditorTextBox.Text);
            CursorPositionText.Text = $"Стр.: {line + 1}  Стб.: {col + 1}  Размер: {totalBytes} байт";
        }

        private bool CheckSaveChanges()
        {
            if (isTextChanged)
            {
                MessageBoxResult result = MessageBox.Show(
                    "Сохранить изменения в файле?",
                    "Сохранение",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    Save_Click(null, null);
                    return !isTextChanged;
                }
                else if (result == MessageBoxResult.No)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        private void UpdateTitle()
        {
            string title = "Поиск по регулярным выражениям";
            if (!string.IsNullOrEmpty(currentFilePath))
            {
                title += $" - {System.IO.Path.GetFileName(currentFilePath)}";
            }
            if (isTextChanged)
            {
                title += "*";
            }
            this.Title = title;
        }

        private void UpdateStatus(string message, int matchesCount)
        {
            StatusText.Text = message;
            StatsText.Text = $"Найдено совпадений: {matchesCount}";
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!CheckSaveChanges())
            {
                e.Cancel = true;
            }
            base.OnClosing(e);
        }
    }

    public class SearchMatch
    {
        public string Text { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public int Length { get; set; }
        public int StartIndex { get; set; }
    }
}