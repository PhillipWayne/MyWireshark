using System.Text;
using System.IO;

namespace ClassLibrary
{
    public class FileManager : IFileManager
    {
        /// <summary>
        /// Кодировка по-умолчанию(1251)
        /// </summary>
        private readonly Encoding _defaultEncoding = Encoding.GetEncoding(1251);

        /// <summary>
        /// Проверка существования файла
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public bool IsExist(string filePath)
        {
            bool isExist = File.Exists(filePath);
            return isExist;
        }

        /// <summary>
        /// Получение содержимого с использованием стандартной Windows-кодировки
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public string GetContent(string filePath)
        {
            return GetContent(filePath, _defaultEncoding);
        }

        /// <summary>
        /// Чтение всего файла
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        public string GetContent(string filePath, Encoding encoding)
        {
            string content = File.ReadAllText(filePath, encoding);
            return content;
        }

        /// <summary>
        /// Сохранение данных в файл с использованием стандартной кодировки
        /// </summary>
        /// <param name="content"></param>
        /// <param name="filePath"></param>
        public void SaveContent(string content, string filePath)
        {
            SaveContent(content, filePath, _defaultEncoding);
        }

        /// <summary>
        /// Сохранениие данных в файл
        /// </summary>
        /// <param name="content"></param>
        /// <param name="filePath"></param>
        /// <param name="encoding"></param>
        public void SaveContent(string content, string filePath, Encoding encoding)
        {
            File.WriteAllText(filePath, content, encoding);
        }
    }
}
