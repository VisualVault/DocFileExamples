using System;
using System.Collections.Generic;
using VVRestApi.Common;
using VVRestApi.Vault;
using System.Configuration;
using VVRestApi.Vault.Library;
using System.IO;
using System.Text.RegularExpressions;

namespace DocFileExamples
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //vaultClient will be null if authentication fails
            var vaultClient = VaultApiLogin();

            const string providerId = "PROV-001670";

            var documentList = GetProviderDocuments(vaultClient, providerId);                     

            foreach (var doc in documentList)
            {
                GetDocumentMetaData(doc);

                GetDocumentRevisionFile(vaultClient, doc);
            }
        }

        private static VaultApi VaultApiLogin()
        {
            var clientSecrets = new ClientSecrets
            {
                BaseUrl = ConfigurationManager.AppSettings["BaseUrl"],
                CustomerAlias = ConfigurationManager.AppSettings["CustomerAlias"],
                DatabaseAlias = ConfigurationManager.AppSettings["DatabaseAlias"],
                ApiKey = ConfigurationManager.AppSettings["ApiKey"],
                ApiSecret = ConfigurationManager.AppSettings["ApiSecret"],
                ApiVersion = "1",
                Scope = "vault",
                OAuthTokenEndPoint = ConfigurationManager.AppSettings["OAuthTokenEndPoint"]
            };

            var vaultClient = new VaultApi(clientSecrets);

            return vaultClient;
        }

        private static List<Document> GetProviderDocuments(VaultApi vaultApi, string providerId)
        {

            var folderPath = $"/Provider Licensing/Providers/{providerId}";

            var docList = vaultApi.Documents.GetDocumentsBySearch(new RequestOptions { Query = $"[folderPath] eq '{folderPath}'", Expand = true });

            //note: you can execute this http get in the browser using this URL
            //https://dcfplads.visualvault.com/api/v1/FLDCF/SAMH/documents?q = folderPath eq '/Provider Licensing/Providers/PROV-001670'

            //search query syntax (OData) can be found at 
            //https://docs.visualvault.com/docs/query-syntax

            return docList;

        }

        private static void GetDocumentMetaData(Document doc)
        {
            if (doc != null)
            {
                //revision neutral Id, always returns latest revision of the document
                Guid documentId = doc.DocumentId;

                //revision specific Id, can be used to get a specific document revision that may not be latest
                Guid revisionId = doc.Id;

                Guid folderId = doc.FolderId;

                string folderPath = doc.FolderPath;

                //id of the file attached to document (each doc revision can have 1 file attached)
                Guid fileId = doc.FileId;

                //file name includes the file extension                
                string fileName = doc.Filename;

                //file extension does not begin with "."
                string fileExtension = doc.Extension;

                //MIME Type such as application/pdf
                string contentType = doc.ContentType;

                //always the create date of revision 1
                DateTime createDate = doc.CreateDate;

                //create date of revision n, for rev 1 will match createDate
                DateTime modifyDate = doc.ModifyDate;

                //revison defaults to integer but can be any unqiue user defined value
                string revision = doc.Revision;
            }
        }

        private static void GetDocumentRevisionFile(VaultApi vaultApi, Document doc)
        {
            //simple example to write file to local disk
            //and skip if file exists and is the same size

            var stream = vaultApi.Files.GetStream(doc.Id);

            var localFilePath = System.IO.Path.GetTempPath();

            if (stream != null)
            {
                //Store the document in the same path as it exists in VisualVault, within the target folder specified in the download configuration settings
                var targetFolderPath = localFilePath + doc.FolderPath;

                //remove any characters not valid for local file system
                var vvFolderPathFolderNamesPattern = @"\/([^\/]+)";
                var folderPathFolderNames = Regex.Matches(targetFolderPath, vvFolderPathFolderNamesPattern);

                if (targetFolderPath.Length + doc.Filename.Length >= 200)
                {
                    //attempt to shorten path if too long for local file system
                    var winFileSystemFolderPathPattern = @".*\\([^\/]+)";
                    var winFolderPath = Regex.Matches(targetFolderPath, winFileSystemFolderPathPattern);

                    //shorten path to avoid exception.  NT file system can only handle 260 char path.
                    targetFolderPath = winFolderPath[0] + folderPathFolderNames[0].ToString().Replace(@"/", @"\") + @"\";
                }

                if (!Directory.Exists(targetFolderPath))
                {
                    Directory.CreateDirectory(targetFolderPath);
                }

                string docPath = targetFolderPath + "\\" + doc.Filename;

                if (File.Exists(docPath))
                {
                    //check file size and if identical then skip
                    //allows restart of downloads
                    var fileInfo = new FileInfo(docPath);
                    if (fileInfo.Length != doc.FileSize)
                    {
                        File.Delete(docPath);
                    }

                    if (!File.Exists(docPath))
                    {
                        using (var fileStream = File.Create(docPath))
                        {
                            //can also read file stream from VV in chunks for large files                                
                            stream.CopyTo(fileStream);
                        }
                    }
                }
            }
        }
    }
}
