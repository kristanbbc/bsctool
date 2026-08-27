using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows;
using NLog;

namespace BBC.BSC.Tool
{
    public class Preparer
    {
        private Logger _logger;
        public Preparer()
        {
            _logger = new Logging().InitLogger();
        }

        public bool PrepareTool(byte[] resource, string outputPath)
        {
            _logger.Trace("Preparing tool to path {0}", outputPath);
            if (File.Exists(outputPath))
            {
                _logger.Trace("Tool path already exists.");
                //check md5
                byte[] existingMd5;
                using (var md5 = SHA256.Create())
                {
                    using (var stream = File.OpenRead(outputPath))
                    {
                        existingMd5 = md5.ComputeHash(stream);
                    }
                }

                //md5 of embedded resource
                byte[] resourceMd5;
                using (var md5 = SHA256.Create())
                {
                    md5.TransformFinalBlock(resource, 0, resource.Length);
                    resourceMd5 = md5.Hash;
                }

                if (existingMd5.SequenceEqual(resourceMd5))
                {
                    _logger.Trace("Tool path exists and SHA256 matches, returning true");
                    return true;
                }

                _logger.Warn("Tool path exists, but SHA256 doesn't match, remove file and retest");
                File.Delete(outputPath);

                return PrepareTool(resource, outputPath);
            }
            else
            {
                try
                {
                    _logger.Trace("Tool doesn't exist, writing out new file");
                    using (FileStream exeFile = new FileStream(outputPath, FileMode.Create))
                    {
                        exeFile.Write(resource, 0, resource.Length);
                    }

                    _logger.Debug("Tool written to {0}, returning true", outputPath);
                    return true;
                }
                catch (IOException ex)
                {
                    _logger.Error("Problem writing out tool. {0}", ex.Message);
                    _ = MessageBox.Show($"Unable to write tool to {outputPath}", "Error in preparing tool", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            return false;
        }
    }
}