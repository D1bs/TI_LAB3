using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace ElGamalLab
{
    public partial class MainWindow : Window
    {
        private string? _inputFilePath;
        private string? _outputFilePath;

        public MainWindow()
        {
            InitializeComponent();
            UpdateModeHint();
            UpdateExecuteButton();
        }
        
        private void Params_TextChanged(object? sender, TextChangedEventArgs e)
        {
            ValidateAndUpdate();
        }

        private void ValidateAndUpdate()
        {
            var errors  = new StringBuilder();
            bool valid  = true;
            
            BigInteger p = 0;
            if (!BigInteger.TryParse(PTextBox?.Text, out p) || p < 2)
            {
                errors.AppendLine("• p должно быть целым числом ≥ 2.");
                valid = false;
                if (PStatusText != null) { PStatusText.Text = "✗"; PStatusText.Foreground = Avalonia.Media.Brushes.Red; }
                if (FindRootsBtn != null) FindRootsBtn.IsEnabled = false;
            }
            else if (!ElGamalCipher.IsPrime(p))
            {
                errors.AppendLine($"• p = {p} не является простым числом.");
                valid = false;
                if (PStatusText != null) { PStatusText.Text = "✗ не простое"; PStatusText.Foreground = Avalonia.Media.Brushes.Red; }
                if (FindRootsBtn != null) FindRootsBtn.IsEnabled = false;
            }
            else
            {
                if (PStatusText != null) { PStatusText.Text = "✓ простое"; PStatusText.Foreground = Avalonia.Media.Brushes.Green; }
                if (FindRootsBtn != null) FindRootsBtn.IsEnabled = true;
            }
            
            BigInteger g = 0;
            if (!BigInteger.TryParse(GTextBox?.Text, out g) || g < 2)
            {
                errors.AppendLine("• g должно быть целым числом ≥ 2.");
                valid = false;
            }
            else if (valid && p >= 2 && ElGamalCipher.IsPrime(p))
            {
                // Проверка что g — первообразный корень mod p
                BigInteger phi = p - 1;
                var factors = ElGamalCipher.PrimeFactors(phi);
                bool isRoot = true;
                foreach (var q in factors)
                {
                    if (ElGamalCipher.FastExp(g, phi / q, p) == 1)
                    { isRoot = false; break; }
                }
                if (g >= p)
                {
                    errors.AppendLine($"• g должно быть в диапазоне [2, p−1] = [2, {p-1}].");
                    valid = false;
                }
                else if (!isRoot)
                {
                    errors.AppendLine($"• g = {g} не является первообразным корнем по модулю p = {p}.");
                    valid = false;
                }
            }
            
            BigInteger x = 0;
            if (!BigInteger.TryParse(XTextBox?.Text, out x))
            {
                errors.AppendLine("• x должно быть целым числом.");
                valid = false;
            }
            else if (valid && p >= 2)
            {
                if (x <= 1 || x >= p - 1)
                {
                    errors.AppendLine($"• x должно быть в диапазоне (1, p−1) = (1, {p-1}).");
                    valid = false;
                }
            }
            
            BigInteger k = 0;
            if (!BigInteger.TryParse(KTextBox?.Text, out k))
            {
                errors.AppendLine("• k должно быть целым числом.");
                valid = false;
            }
            else if (valid && p >= 2)
            {
                if (k <= 1 || k >= p - 1)
                {
                    errors.AppendLine($"• k должно быть в диапазоне (1, p−1) = (1, {p-1}).");
                    valid = false;
                }
                else if (ElGamalCipher.Gcd(k, p - 1) != 1)
                {
                    errors.AppendLine($"• k = {k} должно быть взаимно простым с p−1 = {p-1} (НОД = {ElGamalCipher.Gcd(k, p-1)}).");
                    valid = false;
                }
            }

            // --- Вычисляем y = g^x mod p если всё OK ---
            if (valid && YValueText != null)
            {
                BigInteger y = ElGamalCipher.ComputePublicKey(g, x, p);
                YValueText.Text = y.ToString();
            }
            else if (YValueText != null)
            {
                YValueText.Text = "—";
            }
            
            if (ParamsStatusText != null)
            {
                ParamsStatusText.Text = valid ? "✓ Все параметры корректны." : errors.ToString();
                ParamsStatusText.Foreground = valid
                    ? Avalonia.Media.Brushes.Green
                    : Avalonia.Media.Brushes.Red;
            }

            UpdateExecuteButton();
        }
        
        // Нахождение всех первообразных корней
        private void FindRoots_Click(object? sender, RoutedEventArgs e)
        {
            if (!BigInteger.TryParse(PTextBox?.Text, out BigInteger p) || !ElGamalCipher.IsPrime(p))
            {
                if (RootsTextBox != null) RootsTextBox.Text = "Введите корректное простое p.";
                return;
            }

            try
            {
                List<BigInteger> roots = ElGamalCipher.FindAllPrimitiveRoots(p);
                if (RootsTextBox != null)
                {
                    RootsTextBox.Text = roots.Count > 0
                        ? $"Количество: {roots.Count}  |  {string.Join(", ", roots)}"
                        : "Первообразных корней не найдено.";
                }
                if (roots.Count > 0 && string.IsNullOrWhiteSpace(GTextBox?.Text) && GTextBox != null)
                    GTextBox.Text = roots[0].ToString();

                Log($"Найдено {roots.Count} первообразных корней mod {p}: {string.Join(" ", roots)}");
            }
            catch (Exception ex)
            {
                if (RootsTextBox != null) RootsTextBox.Text = $"Ошибка: {ex.Message}";
            }
        }
        
        private void Mode_Changed(object? sender, RoutedEventArgs e)
        {
            UpdateModeHint();
            _inputFilePath  = null;
            _outputFilePath = null;
            if (InputPathBox  != null) InputPathBox.Text  = "";
            if (OutputPathBox != null) OutputPathBox.Text = "";
            if (CipherOutputBox != null) CipherOutputBox.Text = "";
            UpdateExecuteButton();
        }

        private void UpdateModeHint()
        {
            if (ModeHintText == null) return;
            bool enc = EncryptRadio?.IsChecked == true;
            ModeHintText.Text = enc
                ? "Шифрование: входной файл — любой формат. Выходной — текст: пары чисел (a b)."
                : "Дешифрование: входной файл — зашифрованный текст с парами (a b). Выходной — исходный файл.";
        }
        
        private async void OpenInputFile_Click(object? sender, RoutedEventArgs e)
        {
            bool enc = EncryptRadio?.IsChecked == true;
            var opts = new FilePickerOpenOptions
            {
                Title = enc ? "Выберите файл для шифрования" : "Выберите зашифрованный файл",
                AllowMultiple = false
            };

            var files = await StorageProvider.OpenFilePickerAsync(opts);
            if (files.Count > 0)
            {
                _inputFilePath = files[0].Path.LocalPath;
                if (InputPathBox != null) InputPathBox.Text = _inputFilePath;
                Log($"Открыт: {_inputFilePath}");
                UpdateExecuteButton();
            }
        }

        private async void ChooseOutputFile_Click(object? sender, RoutedEventArgs e)
        {
            bool enc = EncryptRadio?.IsChecked == true;
            var opts = new FilePickerSaveOptions
            {
                Title = enc ? "Сохранить зашифрованный файл" : "Сохранить расшифрованный файл",
                SuggestedFileName = enc ? "encrypted.txt" : "decrypted.bin"
            };

            var file = await StorageProvider.SaveFilePickerAsync(opts);
            if (file != null)
            {
                _outputFilePath = file.Path.LocalPath;
                if (OutputPathBox != null) OutputPathBox.Text = _outputFilePath;
                UpdateExecuteButton();
            }
        }
        
        private void Execute_Click(object? sender, RoutedEventArgs e)
        {
            if (!BigInteger.TryParse(PTextBox?.Text, out BigInteger p) ||
                !BigInteger.TryParse(GTextBox?.Text, out BigInteger g) ||
                !BigInteger.TryParse(XTextBox?.Text, out BigInteger x) ||
                !BigInteger.TryParse(KTextBox?.Text, out BigInteger k))
            {
                Log("Ошибка: проверьте параметры.");
                return;
            }

            if (string.IsNullOrEmpty(_inputFilePath) || string.IsNullOrEmpty(_outputFilePath))
            {
                Log("Ошибка: выберите входной и выходной файлы.");
                return;
            }

            bool enc = EncryptRadio?.IsChecked == true;
            BigInteger y = ElGamalCipher.ComputePublicKey(g, x, p);

            try
            {
                if (enc)
                    DoEncrypt(p, g, y, k);
                else
                    DoDecrypt(p, x);
            }
            catch (Exception ex)
            {
                Log($"Ошибка: {ex.Message}");
            }
        }

        private void DoEncrypt(BigInteger p, BigInteger g, BigInteger y, BigInteger k)
        {
            byte[] data = File.ReadAllBytes(_inputFilePath!);
            Log($"Шифрование: {data.Length} байт, p={p}, g={g}, y={y}, k={k}");

            var pairs = ElGamalCipher.EncryptBytes(data, p, g, y, k);
            string text = ElGamalCipher.SerializeCiphertext(pairs);

            File.WriteAllText(_outputFilePath!, text, Encoding.ASCII);

            // первые 50 пар на экране
            var sb = new StringBuilder();
            int show = Math.Min(pairs.Count, 50);
            for (int i = 0; i < show; i++)
                sb.AppendLine($"{pairs[i].a} {pairs[i].b}");
            if (pairs.Count > 50)
                sb.AppendLine($"... (и ещё {pairs.Count - 50} строк)");

            if (CipherOutputBox != null) CipherOutputBox.Text = sb.ToString();
            if (CipherOutputLabel != null)
                CipherOutputLabel.Text = $"Зашифрованный файл ({pairs.Count} пар a b в 10-й системе):";

            Log($"✓ Шифрование завершено. Пар: {pairs.Count}. Файл: {_outputFilePath}");
        }

        private void DoDecrypt(BigInteger p, BigInteger x)
        {
            string text = File.ReadAllText(_inputFilePath!, Encoding.ASCII);
            var pairs = ElGamalCipher.DeserializeCiphertext(text);
            Log($"Дешифрование: {pairs.Count} пар, p={p}, x={x}");

            byte[] data = ElGamalCipher.DecryptBytes(pairs, p, x);
            File.WriteAllBytes(_outputFilePath!, data);

            // расшифрованные байты в 10-й системе
            var sb = new StringBuilder();
            int show = Math.Min(data.Length, 200);
            for (int i = 0; i < show; i++)
            {
                sb.Append(data[i]);
                sb.Append(' ');
            }
            if (data.Length > 200) sb.Append($"... (и ещё {data.Length - 200} байт)");

            if (CipherOutputBox  != null) CipherOutputBox.Text = sb.ToString();
            if (CipherOutputLabel != null)
                CipherOutputLabel.Text = $"Расшифрованные байты ({data.Length} шт., 10-я система):";

            Log($"✓ Дешифрование завершено. Байт: {data.Length}. Файл: {_outputFilePath}");
        }
        
        private void UpdateExecuteButton()
        {
            if (ExecuteBtn == null) return;
            bool paramsOk = ParamsStatusText?.Text?.StartsWith("✓") == true;
            bool filesOk  = !string.IsNullOrEmpty(_inputFilePath) &&
                            !string.IsNullOrEmpty(_outputFilePath);
            ExecuteBtn.IsEnabled = paramsOk && filesOk;
        }

        private void Log(string msg)
        {
            if (LogBox == null) return;
            LogBox.Text += $"[{DateTime.Now:HH:mm:ss}] {msg}\n";
        }
    }
}
