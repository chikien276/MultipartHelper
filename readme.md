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
    
    //List<LocalMultipartFileInfo> files;
    
    (var model, var files) = await FileUploadHelper
                    .ParseRequestForm(this, "/Your/Temporary/Folder", new YourModel());

    if (!ModelState.IsValid)
    {
        foreach(var file in files)
        {
            // Delete files here
            System.IO.File.Delete(file.TemporaryLocation);
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
    
    var model2 = await FileUploadHelper.ParseRequestForm(this, async (section, formFileInfo) =>
    {
        // This function will be called every time parser got a file 
        using (var fileStream = System.IO.File.Create($"/Path/To/{Guid.NewGuid().ToString()}"))
        {
            await section.Body.CopyToAsync(fileStream);
        }
    }, new StreamUpload());

    if (!ModelState.IsValid)
    {
        foreach(var file in files)
        {
            // Delete files here
            System.IO.File.Delete(file.TemporaryLocation);
        }
        
        return BadRequest();
    }
    
    ...
    
}
```
# TODO
* Make it a custorm parser instead of extension method
* I haven't wrote any UnitTest at all so the goal is near 100% unit test coverage 
