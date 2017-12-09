using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace ChiKien276.AspNetCore.MultipartHelper
{
    public static class FileUploadHelper
    {
        private static readonly int BONDARY_LENGTH_LIMIT = 1024;

        private static readonly int BUFFER_SIZE = 81920;

        public static async Task<(T model, List<LocalMultipartFileInfo> file)> ParseRequestForm<T>(
            this Controller controller, string tempFolder, T model) where T : class
        {
            (var forms, var files) = await ParseRequest(controller.Request, tempFolder);
            await UpdateAndValidateForm(controller, model, forms);
            return (model, files);
        }

        public static async Task<T> ParseRequestForm<T>(
            this Controller controller, 
                Func<MultipartSection, MultipartFileInfo, Task> fileHandler,
                    T model) 
            where T : class
        {
            var forms = await ParseRequest(controller.Request, fileHandler);
            await UpdateAndValidateForm(controller, model, forms);
            return model;
        }

        private static async Task UpdateAndValidateForm<T>(Controller controller, T model, Dictionary<string, StringValues> forms) where T : class
        {
            var formValueProvider = new FormValueProvider(BindingSource.Form,
                                new FormCollection(forms), CultureInfo.CurrentCulture);

            var bindingSuccessful = await controller.TryUpdateModelAsync(model, prefix: "",
                valueProvider: formValueProvider);

            controller.TryValidateModel(model);
        }

        public static async
            Task<(Dictionary<string, StringValues> forms, List<LocalMultipartFileInfo> files)>
                ParseRequest(HttpRequest request, string tempFolder)
        {
            if (tempFolder == null)
            {
                throw new ApplicationException("Request is not a multipart request");
            }
            return await ParseRequest(request, tempFolder, null);
        }

        public static async
            Task<Dictionary<string, StringValues>>
                ParseRequest(HttpRequest request, Func<MultipartSection, MultipartFileInfo, Task> fileHandler)
        {
            return (await ParseRequest(request, "", fileHandler)).Item1;
        }

        private static async
            Task<(Dictionary<string, StringValues>, List<LocalMultipartFileInfo>)>
                ParseRequest(HttpRequest request, string tempLoc, Func<MultipartSection, MultipartFileInfo, Task> fileHandler = null)
        {
            var files = new List<LocalMultipartFileInfo>();

            if (fileHandler == null)
            {
                fileHandler = HandleFileSection;
            }

            if (!MultipartRequestHelper.IsMultipartContentType(request.ContentType))
            {
                throw new InvalidDataException("Request is not a multipart request");
            }

            var formAccumulator = new KeyValueAccumulator();

            var boundary = MultipartRequestHelper.GetBoundary(
                MediaTypeHeaderValue.Parse(request.ContentType),
                BONDARY_LENGTH_LIMIT);

            var reader = new MultipartReader(boundary, request.Body);

            var section = await reader.ReadNextSectionAsync();

            while (section != null)
            {
                var hasContentDispositionHeader =
                    ContentDispositionHeaderValue.TryParse(
                        section.ContentDisposition,
                        out ContentDispositionHeaderValue contentDisposition);

                if (hasContentDispositionHeader)
                {
                    if (MultipartRequestHelper.HasFileContentDisposition(contentDisposition))
                    {
                        var formFile = new LocalMultipartFileInfo()
                        {
                            Name = section.AsFileSection().Name,
                            FileName = section.AsFileSection().FileName,
                            Length = section.Body.Length,
                        };
                        await fileHandler(section, formFile);
                    }
                    else if (MultipartRequestHelper.HasFormDataContentDisposition(contentDisposition))
                    {
                        formAccumulator = await AccumulateForm(formAccumulator, section, contentDisposition);
                    }
                }

                section = await reader.ReadNextSectionAsync();
            }

            return (formAccumulator.GetResults(), files);

            async Task HandleFileSection(MultipartSection fileSection, MultipartFileInfo formFile)
            {
                string targetFilePath;
                var guid = Guid.NewGuid();

                targetFilePath = Path.Combine(tempLoc, guid.ToString());

                using (var targetStream = File.Create(targetFilePath))
                {
                    await fileSection.Body.CopyToAsync(targetStream);
                }

                var tFormFile = new LocalMultipartFileInfo()
                {
                    Name = formFile.Name,
                    Length = formFile.Length,
                    FileName = formFile.FileName,
                    TemporaryLocation = targetFilePath,
                };

                files.Add(tFormFile);
            }
        }

        private static async Task<KeyValueAccumulator> AccumulateForm
            (KeyValueAccumulator formAccumulator,
            MultipartSection section,
            ContentDispositionHeaderValue contentDisposition)
        {
            var key = MultipartRequestHelper.RemoveQuotes(contentDisposition.Name.Value);
            var encoding = MultipartRequestHelper.GetEncoding(section);
            using (var streamReader = new StreamReader(
                section.Body,
                encoding,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: BUFFER_SIZE,
                leaveOpen: true))
            {
                var value = await streamReader.ReadToEndAsync();
                if (String.Equals(value, "undefined", StringComparison.OrdinalIgnoreCase))
                {
                    value = String.Empty;
                }
                formAccumulator.Append(key, value);

                if (formAccumulator.ValueCount > FormReader.DefaultValueCountLimit)
                {
                    throw new InvalidDataException(
                        $"Form key count limit {FormReader.DefaultValueCountLimit} exceeded.");
                }
            }

            return formAccumulator;
        }

    }

    public class MultipartFileInfo
    {
        public long Length { get; set; }

        public string FileName { get; set; }

        public string Name { get; set; }
    }

    public class LocalMultipartFileInfo : MultipartFileInfo
    {
        public string TemporaryLocation { get; set; }
    }
}
