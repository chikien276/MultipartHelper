# Usage
At first, you have to verify that is a multipart request
```
if (!MultipartRequestHelper.IsMultipartContentType(Request.ContentType))
{
    return BadRequest();
}
```
## Use temporary folder

Uploaded files is temporarily saved in folder `/Your/Temporary/Folder` at `LocalMultipartFileInfo.TemporaryLocaltion`. `LocalMultipartFileInfo` also contains `Length`, `FileName` and `Name` of multipart file.

```
[HttpPost("Upload")]
[DisableFormValueModelBinding]
public async Task<IActionResult> Upload()
{
    ...
    
    Dictionary<string, StringValues> forms;
    List<LocalMultipartFileInfo> files;

    (forms, files) = await FileUploadHelper.ParseRequest(Request, "/Your/Temporary/Folder");
 
    var formValueProvider = new FormValueProvider(BindingSource.Form, new FormCollection(forms), CultureInfo.CurrentCulture);
    YourModel model = new YourModel();
    var formValueProvider = new FormValueProvider(BindingSource.Form,
                    new FormCollection(forms), CultureInfo.CurrentCulture);

    if (!bindingSuccessful || !ModelState.IsValid)
    {
        foreach(var file in files)
        {
            // Delete files here
        }
        
        return BadRequest();
    }
    
    ...
}
```
## Handle file stream yourself

```
[HttpPost("Upload")]
[DisableFormValueModelBinding]
public async Task<IActionResult> Upload()
{
    ...
    
    Dictionary<string, StringValues> forms;

    (forms, files) = await FileUploadHelper.ParseRequest(Request, (section, formFileInfo) =>
    {        
        using (var fileStream = File.Create("/Path/To/File"))
        {
            section.Body.CopyTo(fileStream);
        }
        return Task.CompletedTask;
    });
 
    var formValueProvider = new FormValueProvider(BindingSource.Form, new FormCollection(forms), CultureInfo.CurrentCulture);
    YourModel model = new YourModel();
    var formValueProvider = new FormValueProvider(BindingSource.Form,
                    new FormCollection(forms), CultureInfo.CurrentCulture);

    if (!bindingSuccessful || !ModelState.IsValid)
    {
        foreach(var file in files)
        {
            // Delete files here
        }
        
        return BadRequest();
    }
    
    ...
    
}
```
