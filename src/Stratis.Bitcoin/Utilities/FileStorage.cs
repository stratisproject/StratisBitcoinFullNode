using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Utilities.FileStorage
{
    /// <summary>
    /// Class providing methods to save objects as files on the file system.
    /// </summary>
    /// <typeparam name="T">The type of object to be stored in the file system.</typeparam>
    public class FileStorage<T> where T : new()
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

            // Create a folder if none exists.
            Directory.CreateDirectory(folderPath);
        }

        /// <summary>
        /// Saves an object to a file.
        /// </summary>
        /// <param name="toSave">Object to save as a file.</param>
        /// <param name="fileName">Name of the file to be saved.</param>
        public void SaveToFile(T toSave, string fileName)
        {
            Guard.NotEmpty(fileName, nameof(fileName));
            Guard.NotNull(toSave, nameof(toSave));

            string filePath = Path.Combine(this.FolderPath, fileName);
            File.WriteAllText(filePath, JsonConvert.SerializeObject(toSave, Formatting.Indented));
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

            var filesPaths = this.GetFilesPaths(fileExtension);
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

            // Load the file from the file system.
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

            List<T> files = new List<T>();
            foreach (var filePath in filesPaths)
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
