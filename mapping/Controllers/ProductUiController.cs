using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KP_InternalSystem.Models;
using KP_InternalSystem.Data; 
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Text;

namespace KP_InternalSystem.Controllers
{
    public class ProductUiController : Controller
    {
        private readonly RatDbContext _context;

        public ProductUiController(RatDbContext context)
        {
            _context = context;
        }

        // GET: ProductUi (Hapus parameter divisionId)
        public async Task<IActionResult> Index(string search, string status)
        {
            var query = _context.Products
                .Include(p => p.ProductAliases) // Hapus Include Division
                .AsQueryable();

            // Filter Pencarian
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p => p.ProductName.Contains(search) || p.ProductCode.Contains(search));
            }

            // HAPUS FILTER DIVISI DISINI

            // Filter Status
            if (!string.IsNullOrEmpty(status))
            {
                bool isActive = status.ToUpper() == "ACTIVE";
                query = query.Where(p => p.IsActive == isActive);
            }

            var products = await query.OrderBy(p => p.ProductName).ToListAsync();

            // Data Pendukung View (Hapus ViewBag.Divisions)
            ViewBag.TotalProducts = await _context.Products.CountAsync();
            ViewBag.ActiveProducts = await _context.Products.CountAsync(p => p.IsActive);
            
            ViewBag.SelectedSearch = search;
            ViewBag.SelectedStatus = status;

            return View(products);
        }

        [HttpGet]
        public async Task<IActionResult> GetProductsJson(string search, string status)
        {
            // Ambil data query dasar
            var query = _context.Products.Include(p => p.ProductAliases).AsQueryable();

            // Filter Search
            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                query = query.Where(p => p.ProductName.ToLower().Contains(search) || p.ProductCode.ToLower().Contains(search));
            }

            // Filter Status
            if (!string.IsNullOrEmpty(status))
            {
                bool isActive = status.ToUpper() == "ACTIVE";
                query = query.Where(p => p.IsActive == isActive);
            }

            // Ambil data dari DB
            var dataList = await query.OrderBy(p => p.ProductName).ToListAsync();

            // Format data biar ringan dikirim ke Javascript
            var result = dataList.Select(p => new {
                id = p.ProductId,
                name = p.ProductName,
                code = p.ProductCode,
                isActive = p.IsActive,
                aliases = p.ProductAliases.Select(a => a.AliasName).ToList() // Ambil list alias
            });

            return Json(result);
        }

        [HttpPost]
        public async Task<IActionResult> SaveProduct(int? id, string name, string code, string aliasStr, bool isActive)
        {
            // Parameter divisionId DIHAPUS dari method ini
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                Product product;

                if (id == null || id == 0)
                {
                    // CREATE
                    product = new Product
                    {
                        ProductName = name,
                        ProductCode = code,
                        // DivisionId dihapus
                        IsActive = isActive 
                    };
                    _context.Products.Add(product);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    // UPDATE
                    product = await _context.Products
                        .Include(p => p.ProductAliases)
                        .FirstOrDefaultAsync(p => p.ProductId == id);

                    if (product == null) return NotFound();

                    product.ProductName = name;
                    product.ProductCode = code;
                    // DivisionId dihapus
                    product.IsActive = isActive; 
                    
                    _context.ProductAliases.RemoveRange(product.ProductAliases);
                }

                if (!string.IsNullOrEmpty(aliasStr))
                {
                    var aliases = aliasStr.Split(',')
                        .Select(a => a.Trim())
                        .Where(a => !string.IsNullOrEmpty(a))
                        .Distinct()
                        .Select(a => new ProductAlias
                        {
                            AliasName = a,
                            ProductId = product.ProductId
                        }).ToList();

                    if (aliases.Any())
                    {
                        _context.ProductAliases.AddRange(aliases);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { message = "Success" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest("Error: " + ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
            }
            return Ok(new { message = "Deleted" });
        }

        public async Task<IActionResult> ExportExcel(string search, string status)
        {
            var query = _context.Products
                .Include(p => p.ProductAliases)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search)) query = query.Where(p => p.ProductName.Contains(search) || p.ProductCode.Contains(search));
            // Filter Division DIHAPUS
            if (!string.IsNullOrEmpty(status))
            {
                bool isActive = status.ToUpper() == "ACTIVE";
                query = query.Where(p => p.IsActive == isActive);
            }

            var data = await query.ToListAsync();

            var builder = new StringBuilder();
            builder.AppendLine("Product Name,Code,Status,Aliases"); // Header Division Dihapus

            foreach (var item in data)
            {
                var aliases = string.Join(";", item.ProductAliases.Select(a => a.AliasName));
                var statusStr = item.IsActive ? "ACTIVE" : "INACTIVE";
                // Kolom Division Dihapus dari CSV
                builder.AppendLine($"\"{item.ProductName}\",\"{item.ProductCode}\",\"{statusStr}\",\"{aliases}\"");
            }

            return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", "MasterProduct.csv");
        }
    }
}