using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ets2SaveEditor.Core.Domain.TypedUnits;
using Ets2SaveEditor.Core.Persistence;
using Ets2SaveEditor.Core.Sii;
using Ets2SaveEditor.Core.Sii.Model;

namespace Ets2SaveEditor.Core.Domain
{
    /// <summary>
    /// Aggregates a single <c>.sii</c> save file: its format, parsed document
    /// and convenient typed accessors. Loading never writes to disk; saving
    /// serializes and (optionally) backs up the original first.
    /// </summary>
    public class SaveFile
    {
        /// <summary>File system path of the save file.</summary>
        public string Path { get; set; }

        /// <summary>Detected container format.</summary>
        public SiiFormat Format { get; set; }

        /// <summary>Parsed SII document.</summary>
        public SiiDocument Document { get; set; }

        /// <summary>Whether a document is currently loaded.</summary>
        public bool IsLoaded { get; set; }

        public SaveFile()
        {
        }

        public SaveFile(string path)
        {
            Path = path;
        }

        /// <summary>
        /// Loads and parses the save file at <paramref name="path"/> (or the
        /// previously set <see cref="Path"/>). Throws <see cref="SiiFormatException"/>
        /// on any failure and never modifies the original file.
        /// </summary>
        public void Load(string path)
        {
            if (!string.IsNullOrEmpty(path)) Path = path;
            if (string.IsNullOrEmpty(Path))
            {
                throw new SiiFormatException("No save file path specified.");
            }

            byte[] raw;
            try
            {
                raw = File.ReadAllBytes(Path);
            }
            catch (Exception ex)
            {
                IsLoaded = false;
                throw new SiiFormatException("Failed to read save file: " + Path, ex);
            }

            Format = SiiFormatDetector.Detect(raw);

            string text;
            try
            {
                text = SiiFormatDetector.Decode(raw, Format);
            }
            catch (SiiFormatException)
            {
                IsLoaded = false;
                throw;
            }
            catch (Exception ex)
            {
                IsLoaded = false;
                throw new SiiFormatException("Failed to decode save file: " + Path, ex);
            }

            try
            {
                Document = SiiParser.Parse(text);
            }
            catch (Exception ex)
            {
                IsLoaded = false;
                throw new SiiFormatException("Failed to parse SII document: " + Path, ex);
            }

            IsLoaded = true;
        }

        /// <summary>
        /// Serializes the loaded document and writes it to <paramref name="path"/>
        /// (or the previously set <see cref="Path"/>). When <paramref name="backup"/>
        /// is true, a timestamped backup of the target is created first. The
        /// original file is never overwritten if serialization fails.
        /// </summary>
        public void Save(string path, bool backup)
        {
            if (!IsLoaded || Document == null)
            {
                throw new SiiFormatException("Cannot save: no document is loaded.");
            }

            string target = string.IsNullOrEmpty(path) ? Path : path;
            if (string.IsNullOrEmpty(target))
            {
                throw new SiiFormatException("Cannot save: no target path specified.");
            }

            string serialized;
            try
            {
                serialized = SiiSerializer.Serialize(Document);
            }
            catch (Exception ex)
            {
                // Never write the original file if serialization fails.
                throw new SiiFormatException("Failed to serialize document; original file left untouched.", ex);
            }

            try
            {
                if (backup)
                {
                    BackupService.Backup(target);
                }
                File.WriteAllText(target, serialized, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                throw new SiiFormatException("Failed to write save file: " + target, ex);
            }

            Path = target;
        }

        // ---- Convenience typed accessors ----

        public Bank GetBank() => Bank.FromDocument(Document);
        public Player GetPlayer() => Player.FromDocument(Document);
        public List<Garage> GetGarages() => Garage.AllFromDocument(Document);
        public List<Vehicle> GetVehicles() => Vehicle.AllFromDocument(Document);
        public List<Trailer> GetTrailers() => Trailer.AllFromDocument(Document);
        public Economy GetEconomy() => Economy.FromDocument(Document);
        public List<JobOffer> GetFreightJobs() => JobOffer.AllFromDocument(Document);
    }
}
