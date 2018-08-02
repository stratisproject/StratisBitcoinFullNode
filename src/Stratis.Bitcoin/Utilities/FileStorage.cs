using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Class providing methods to save objects as files on the file system.
    /// </summary>
    /// <typeparam name="T">The type of object to be stored in the file system.</typeparam>
    public sealed class FileStorage<T> where T : new()
    {
        /// <summary> Gets the folder path. </summary>
        public string FolderPath { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileStorage{T}"/> class.
        /// </summary>
        /// <param name="folderPath">The path of the folder in which the files are to be stored.</param>
        public FileStorage(string folderPath)
        {
            Guard.NotEmpty(folderPath, nameof(folderPath));

            this.FolderPath = folderPath;

            // Create a folder if none exists.
            Directory.CreateDirectory(folderPath);
        }

        /// <summary>
        /// Saves an object to a file, optionally keeping a backup of it.
        /// </summary>
        /// <param name="toSave">Object to save as a file.</param>
        /// <param name="fileName">Name of the file to be saved.</param>
        /// <param name="saveBackupFile">A value indicating whether to save a backup of the file.</param>
        public void SaveToFile(T toSave, string fileName, bool saveBackupFile = false)
        {
            Guard.NotEmpty(fileName, nameof(fileName));
            Guard.NotNull(toSave, nameof(toSave));

            string filePath = Path.Combine(this.FolderPath, fileName);
            long uniqueId = DateTime.UtcNow.Ticks;
            string newFilePath = $"{filePath}.{uniqueId}.new";
            string tempFilePath = $"{filePath}.{uniqueId}.temp";

            File.WriteAllText(newFilePath, JsonConvert.SerializeObject(toSave, Formatting.Indented));

            // If the file does not exist yet, create it.
            if (!File.Exists(filePath))
            {
                File.Move(newFilePath, filePath);

                if (saveBackupFile)
                {
                    File.Copy(filePath, $"{filePath}.bak", true);
                }

                return;
            }

            if (saveBackupFile)
            {
                File.Copy(filePath, $"{filePath}.bak", true);
            }

            // Delete the file and rename the temp file to that of the target file.
            File.Move(filePath, tempFilePath);
            File.Move(newFilePath, filePath);

            try
            {
                File.Delete(tempFilePath);
            }
            catch (IOException)
            {
                // Marking the file for deletion in the future.
                File.Move(tempFilePath, $"{ filePath}.{ uniqueId}.del");
            }
        }

        /// <summary>
        /// Checks whether a file with the specified name exists in the folder.
        /// </summary>
        /// <param name="fileName">The name of the file to look for.</param>
        /// <returns>A value indicating whether the file exists in the file system.</returns>
        public bool Exists(string fileName)
        {
            Guard.NotEmpty(fileName, nameof(fileName));

            string filePath = Path.Combine(this.FolderPath, fileName);
            return File.Exists(filePath);
        }

        /// <summary>
        /// Gets the paths of the files with the specified extension.
        /// </summary>
        /// <param name="fileExtension">The file extension.</param>
        /// <returns>A list of paths for files with the specified extension.</returns>
        public IEnumerable<string> GetFilesPaths(string fileExtension)
        {
            Guard.NotEmpty(fileExtension, nameof(fileExtension));
            return Directory.EnumerateFiles(this.FolderPath, $"*.{fileExtension}", SearchOption.TopDirectoryOnly);
        }

        /// <summary>
        /// Gets the names of files with the specified extension.
        /// </summary>
        /// <param name="fileExtension">The file extension.</param>
        /// <returns>A list of filenames with the specified extension.</returns>
        public IEnumerable<string> GetFilesNames(string fileExtension)
        {
            Guard.NotEmpty(fileExtension, nameof(fileExtension));

            IEnumerable<string> filesPaths = this.GetFilesPaths(fileExtension);
            return filesPaths.Select(p => Path.GetFileName(p));
        }

        /// <summary>
        /// Loads an object from the file in which it is persisted.
        /// </summary>
        /// <param name="fileName">The name of the file to load.</param>
        /// <returns>An object of type <see cref="T"/>.</returns>
        /// <exception cref="FileNotFoundException">Indicates that no file with this name was found.</exception>
        public T LoadByFileName(string fileName)
        {
            Guard.NotEmpty(fileName, nameof(fileName));

            string filePath = Path.Combine(this.FolderPath, fileName);

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"No wallet file found at {filePath}");

            return JsonConvert.DeserializeObject<T>(File.ReadAllText(filePath));
        }

        /// <summary>
        /// Loads all the objects that have file with the specified extension.
        /// </summary>
        /// <param name="fileExtension">The file extension.</param>
        /// <returns>A list of objects of type <see cref="T"/> whose persisted files have the specified extension. </returns>
        public IEnumerable<T> LoadByFileExtension(string fileExtension)
        {
            Guard.NotEmpty(fileExtension, nameof(fileExtension));

            // Get the paths of files with the extension
            IEnumerable<string> filesPaths = this.GetFilesPaths(fileExtension);

            var files = new List<T>();
            foreach (string filePath in filesPaths)
            {
                string fileName = Path.GetFileName(filePath);

                // Load the file into the object of type T.
                T loadedFile = this.LoadByFileName(fileName);
                files.Add(loadedFile);
            }

            return files;
        }
    }
}
