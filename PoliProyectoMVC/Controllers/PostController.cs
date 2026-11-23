using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using PoliProyectoMVC.Data;
using PoliProyectoMVC.Models;
using PoliProyectoMVC.Models.ViewModels;

namespace PoliProyectoMVC.Controllers;

public class PostController : Controller
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly string[] _allowedExtension = { ".jpg", ".jpeg", ".png" };

    public PostController(AppDbContext context, IWebHostEnvironment webHostEnvironment)
    {
        _context = context;
        _webHostEnvironment = webHostEnvironment;
    }

    // GET: Posts/Create
    [HttpGet]
    public IActionResult Create()
    {
        //ViewData["Category"] = new SelectList(_context.Categori
        //es, "Id", "Name");
        var postViewModel = new PostViewModel();
        postViewModel.Categories = _context.Categories.Select(c => new SelectListItem
        {
            Value = c.Id.ToString(),
            Text = c.Name
        }).ToList();

        return View(postViewModel);
    }

    [HttpGet]
    public IActionResult Index(int? categoryId)
    {
        var postQuery = _context.Posts.Include(p => p.Category).AsQueryable();
        if (categoryId.HasValue)
        {
            postQuery = postQuery.Where(p => p.CategoryId == categoryId);
        }
        var posts = postQuery.ToList();

        ViewBag.Categories = _context.Categories.ToList();

        return View(posts);
    }

    //[HttpGet]
    //public IActionResult Index()
    //{
    //    return View();
    //}

    [HttpPost]
    public async Task<IActionResult> Create(PostViewModel postViewModel)
    {
        if (ModelState.IsValid)
        {
            var inputFileExtension = Path.GetExtension(postViewModel.FeatureImage.FileName).ToLower();
            bool isAllowed = _allowedExtension.Contains(inputFileExtension);
            if (!isAllowed)
            {
                ModelState.AddModelError("Image", "Invalid image format. Allowed formats are .jpg, .jpeg, .png");
                return View(postViewModel);
            }
            postViewModel.Post.FeatureImagePath = await UploadFileToFolder(postViewModel.FeatureImage);
            _context.Posts.Add(postViewModel.Post);
            _context.SaveChanges();

            return RedirectToAction("Index");
        }

        // Esta es la lista de categorias
        postViewModel.Categories = _context.Categories.Select(c => new SelectListItem
        {
            Value = c.Id.ToString(),
            Text = c.Name
        }).ToList();

        return View(postViewModel);
    }

    private async Task<string> UploadFileToFolder(IFormFile file)
    {
        var inputFileExtension = Path.GetExtension(file.FileName);
        var fileName = Guid.NewGuid().ToString() + inputFileExtension;
        var wwwRootPath = _webHostEnvironment.WebRootPath;
        var imagesFolderPath = Path.Combine(wwwRootPath, "images");

        if (!Directory.Exists(imagesFolderPath))
        {
            Directory.CreateDirectory(imagesFolderPath);
        }

        var filePath = Path.Combine(imagesFolderPath, fileName);

        try
        {
            await using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }
        }
        catch (Exception ex)
        {
            // Log the exception if needed.
            return "Error Uploading Image: " + ex.Message;
        }

        return "/images/" + fileName;
    }

    public JsonResult AddComment([FromBody] Comment comment)
    {
        //if (ModelState.IsValid)
        //{
        comment.CommentDate = DateTime.Now;
        _context.Comments.Add(comment);
        _context.SaveChanges();

        return Json(new
        {
            userName = comment.UserName,
            commentDate = comment.CommentDate.ToString("MMMM dd, yyyy"),
            content = comment.Content
        });
        //}

        return Json(new { success = false, message = "Invalid data" });
    }

    [HttpGet]
    public IActionResult Detail(int id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var post = _context.Posts.Include(p => p.Category).Include(p => p.Comments)
            .FirstOrDefault(p => p.Id == id);

        if (post == null)
        {
            return NotFound();
        }
        return View(post);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var postFromDb = await _context.Posts.FirstOrDefaultAsync(p => p.Id == id);

        if (postFromDb == null)
        {
            return NotFound();
        }

        EditViewModel editViewModel = new EditViewModel
        {
            Post = postFromDb,
            Categories = _context.Categories.Select(c =>
            new SelectListItem()
            {
                Value = c.Id.ToString(),
                Text = c.Name
            }
            ).ToList()
        };
        return View(editViewModel);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(EditViewModel editViewModel)
    {
        if (!ModelState.IsValid)
        {
            return View(editViewModel);
        }

        var postFromDb = await _context.Posts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == editViewModel.Post.Id);

        if (postFromDb == null)
        {
            return NotFound();
        }

        if (editViewModel.FeatureImage != null)
        {
            var inputFileExtension = Path.GetExtension
            (editViewModel.FeatureImage.FileName).ToLower();
            bool isAllowed = _allowedExtension.Contains(inputFileExtension);

            if (!isAllowed)
            {
                ModelState.AddModelError("", "Invalid image format. Allowed formats are .jpg, .jpeg, .png");
                return View(editViewModel);
            }

            var existingFilePath = Path.Combine(_webHostEnvironment.WebRootPath, "images",
            Path.GetFileName(postFromDb.FeatureImagePath));

            if (System.IO.File.Exists(existingFilePath))
            {
                System.IO.File.Delete(existingFilePath);
            }

            editViewModel.Post.FeatureImagePath = await UploadFileToFolder(editViewModel.FeatureImage);
        }
        else
        {
            editViewModel.Post.FeatureImagePath = postFromDb.FeatureImagePath;
        }

        _context.Posts.Update(editViewModel.Post);
        await _context.SaveChangesAsync();
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var postFromDb = await _context.Posts.FirstOrDefaultAsync(p => p.Id == id);
        if (postFromDb == null)
        {
            return NotFound();
        }
        return View(postFromDb);
    }

    [HttpPost]
    public async Task<IActionResult> DeleteConfirm(int id)
    {
        if (id < 0)
        {
            return BadRequest();
        }

        var postFromDb = await _context.Posts.FindAsync(id);
        if (postFromDb == null)
        {
            return NotFound();
        }
        if (!string.IsNullOrEmpty(postFromDb.FeatureImagePath))
        {
            var imagePath = Path.Combine(_webHostEnvironment.WebRootPath, "images", Path.GetFileName(postFromDb.FeatureImagePath));
            if (System.IO.File.Exists(imagePath))
            {
                System.IO.File.Delete(imagePath);
            }
        }
        _context.Posts.Remove(postFromDb);
        await _context.SaveChangesAsync();
        return RedirectToAction("Index");
    }
}