using System;
using System.Collections.Generic;
using System.Numerics;

namespace ElGamalLab
{
    public class ElGamalCipher
    {
        // ─── Алгоритм быстрого возведения в степень по модулю ───────────────────
        // x = a^z mod n
        public static BigInteger FastExp(BigInteger a, BigInteger z, BigInteger n)
        {
            BigInteger a1 = a % n;
            BigInteger z1 = z;
            BigInteger x  = 1;

            while (z1 != 0)
            {
                while (z1 % 2 == 0)
                {
                    z1 = z1 / 2;
                    a1 = (a1 * a1) % n;
                }
                z1 = z1 - 1;
                x  = (x * a1) % n;
            }
            return x;
        }

        // ─── Проверка на простоту (детерминированный тест) ──────────────────────
        public static bool IsPrime(BigInteger n)
        {
            if (n < 2)  return false;
            if (n == 2) return true;
            if (n % 2 == 0) return false;
            for (BigInteger i = 3; i * i <= n; i += 2)
                if (n % i == 0) return false;
            return true;
        }
        
        public static BigInteger Gcd(BigInteger a, BigInteger b)
        {
            while (b != 0) { var t = b; b = a % b; a = t; }
            return a;
        }
        
        public static List<BigInteger> PrimeFactors(BigInteger n)
        {
            var factors = new List<BigInteger>();
            for (BigInteger i = 2; i * i <= n; i++)
            {
                if (n % i == 0)
                {
                    factors.Add(i);
                    while (n % i == 0) n /= i;
                }
            }
            if (n > 1) factors.Add(n);
            return factors;
        }

        // ─── Нахождение всех первообразных корней по модулю p ────────────────────
        // g является первообразным корнем mod p, если:
        //   g^((p-1)/q) ≠ 1 (mod p)  для каждого простого делителя q числа (p-1)
        // и g^(p-1) = 1 (mod p) (теорема Ферма)
        public static List<BigInteger> FindAllPrimitiveRoots(BigInteger p)
        {
            if (!IsPrime(p))
                throw new ArgumentException("p должно быть простым числом.");

            BigInteger phi = p - 1;                      // φ(p) = p-1 для простого p
            var primeFactors = PrimeFactors(phi);        // все простые делители φ(p)
            var roots = new List<BigInteger>();

            for (BigInteger g = 2; g < p; g++)
            {
                bool isRoot = true;
                foreach (var q in primeFactors)
                {
                    // g — первообразный корень ⟺ g^((p-1)/q) ≠ 1 (mod p) для всех q
                    if (FastExp(g, phi / q, p) == 1)
                    {
                        isRoot = false;
                        break;
                    }
                }
                if (isRoot) roots.Add(g);
            }
            return roots;
        }

        // Генерация открытого ключа y = g^x mod p 
        public static BigInteger ComputePublicKey(BigInteger g, BigInteger x, BigInteger p)
            => FastExp(g, x, p);

        // Проверка корректности k
        public static bool ValidateK(BigInteger k, BigInteger p)
            => k > 1 && k < p - 1 && Gcd(k, p - 1) == 1;

        // Шифрование одного байта 
        // Вход:  m ∈ [0, p-1],  открытый ключ Ko = (p, g, y),  секретное k
        // Выход: (a, b), где  a = g^k mod p,  b = y^k * m mod p
        public static (BigInteger a, BigInteger b) EncryptByte(
            byte m, BigInteger p, BigInteger g, BigInteger y, BigInteger k)
        {
            BigInteger a = FastExp(g, k, p);
            BigInteger b = (FastExp(y, k, p) * m) % p;
            return (a, b);
        }

        //Дешифрование одного байта 
        // m = b * a^(-x) mod p  = b * a^(p-1-x) mod p  (малая теорема Ферма)
        public static byte DecryptByte(
            BigInteger a, BigInteger b, BigInteger p, BigInteger x)
        {
            // a^(-x) mod p = a^(p-1-x) mod p  (т.к. a^(p-1)=1 mod p для простого p)
            BigInteger aInvX = FastExp(a, p - 1 - x, p);
            BigInteger m = (b * aInvX) % p;
            return (byte)m;
        }

        // Шифрование массива байтов 
        // Возвращает список пар (a, b) для каждого байта
        public static List<(BigInteger a, BigInteger b)> EncryptBytes(
            byte[] data, BigInteger p, BigInteger g, BigInteger y, BigInteger k)
        {
            var result = new List<(BigInteger, BigInteger)>(data.Length);
            foreach (byte m in data)
                result.Add(EncryptByte(m, p, g, y, k));
            return result;
        }

        // дешифрование списка пар (a, b) 
        public static byte[] DecryptBytes(
            List<(BigInteger a, BigInteger b)> pairs, BigInteger p, BigInteger x)
        {
            var result = new byte[pairs.Count];
            for (int i = 0; i < pairs.Count; i++)
                result[i] = DecryptByte(pairs[i].a, pairs[i].b, p, x);
            return result;
        }

        //Сериализация шифротекста в строку
        // Формат: каждая пара на отдельной строке: "a b"
        public static string SerializeCiphertext(List<(BigInteger a, BigInteger b)> pairs)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var (a, b) in pairs)
                sb.AppendLine($"{a} {b}");
            return sb.ToString();
        }

        //  Десериализация шифротекста из строки 
        public static List<(BigInteger a, BigInteger b)> DeserializeCiphertext(string text)
        {
            var result = new List<(BigInteger, BigInteger)>();
            foreach (var line in text.Split('\n', '\r'))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                var parts = trimmed.Split(' ');
                if (parts.Length != 2)
                    throw new FormatException($"Неверный формат строки: '{trimmed}'");
                result.Add((BigInteger.Parse(parts[0]), BigInteger.Parse(parts[1])));
            }
            return result;
        }
    }
}
