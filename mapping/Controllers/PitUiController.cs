using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KP_InternalSystem.Models;
using KP_InternalSystem.Data; 
using Microsoft.AspNetCore.Hosting;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.IO;
using System.Text;

namespace KP_InternalSystem.Controllers
{
    public class PitUiController : Controller
    {
        private readonly RatDbContext _context;
        private readonly IWebHostEnvironment _env;

        public PitUiController(RatDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // HELPER LOG KE DATABASE 
        private async Task RegisterActivity(string type, string msg)
        {
            try 
            { 
                var log = new UserActivityLog 
                { 
                    ActionType = type, 
                    Message = msg, 
                    CreatedAt = DateTime.Now 
                };
                _context.UserActivityLogs.Add(log);
                await _context.SaveChangesAsync();
            } catch { }
        }

        // 1. INDEX
        public async Task<IActionResult> Index()
        {
            var lastLog = await _context.UserActivityLogs.OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync();

            if (lastLog != null) {
                ViewBag.LastUpdateStr = lastLog.CreatedAt.ToString("dd MMM yyyy HH:mm");
                ViewBag.LastUpdateMsg = lastLog.Message;
            } else {
                ViewBag.LastUpdateStr = "-";
                ViewBag.LastUpdateMsg = "No activity recorded yet.";
            }

            ViewBag.Divisions = await _context.Divisions.OrderBy(d => d.DivisionName).ToListAsync();
            ViewBag.Locations = await _context.Locations.OrderBy(l => l.LocationName).ToListAsync();
            ViewBag.AllDepartments = await _context.Departments.OrderBy(d => d.DepartmentName).ToListAsync();
            
            var pits = await GetFilteredPitsQuery(null, null, null, null);
            return View(pits);
        }

        // 2. API LIVE SEARCH
        [HttpGet]
        public async Task<JsonResult> GetPitsJson(string search, string filterDiv, string filterDept, string filterStatus)
        {
            var data = await GetFilteredPitsQuery(search, filterDiv, filterDept, filterStatus);
            return Json(data);
        }

        // 3. EXPORT EXCEL
        public async Task<IActionResult> ExportExcel(string search, string filterDiv, string filterDept, string filterStatus)
        {
            var data = await GetFilteredPitsQuery(search, filterDiv, filterDept, filterStatus);
            var builder = new StringBuilder();
            builder.AppendLine("Pit Code,Official Name,Alias,Division,Department,Location,Status");
            foreach (dynamic item in data) {
                string line = $"\"{item.Code}\",\"{item.Name}\",\"{item.Alias}\",\"{item.Div}\",\"{item.Dept}\",\"{item.Loc}\",\"{item.Status}\"";
                builder.AppendLine(line);
            }
            var encoding = new UTF8Encoding(true);
            string fileName = $"PitMapping_Data_{DateTime.Now:yyyyMMdd_HHmm}.csv";
            return File(encoding.GetBytes(builder.ToString()), "text/csv", fileName);
        }

        // Helper Query (Digunakan oleh Index & Live Search)
        private async Task<List<object>> GetFilteredPitsQuery(string search, string div, string dept, string status)
        {
            var query = _context.Pits
                .Include(p => p.Location)
                .Include(p => p.Department).ThenInclude(d => d.Division)
                .Include(p => p.PitAliases) // Include Alias
                .AsQueryable();

            // 1. Filter Pencarian
            if (!string.IsNullOrEmpty(search)) {
                string s = search.ToLower();
                query = query.Where(p => p.PitNameOfficial.ToLower().Contains(s) 
                    || p.PitCode.ToLower().Contains(s) 
                    || p.PitAliases.Any(a => a.AliasName.ToLower().Contains(s)));
            }
            
            // 2. Filter Dropdown
            if (!string.IsNullOrEmpty(div)) query = query.Where(p => p.Department.Division.DivisionName == div);
            if (!string.IsNullOrEmpty(dept)) query = query.Where(p => p.Department.DepartmentName == dept);
            if (!string.IsNullOrEmpty(status)) query = query.Where(p => p.Status.Trim() == status);

            // 3. SORTING ABJAD (A-Z) BERDASARKAN NAMA PIT
            var rawData = await query
                .OrderBy(p => p.PitNameOfficial) // 
                .ToListAsync();

            // 4. Mapping Data ke Object UI
            var result = rawData.Select(p => new {
                Id = p.PitId, 
                Code = p.PitCode, 
                Name = p.PitNameOfficial,
                
                Alias = p.PitAliases.Any() 
                        ? string.Join(", ", p.PitAliases.Select(a => a.AliasName)) 
                        : "-",

                Div = p.Department.Division.DivisionName,
                Dept = p.Department.DepartmentName,
                Loc = p.Location.LocationName,
                Status = (p.Status ?? "INACTIVE").ToUpper()
            }).ToList();

            return result.Cast<object>().ToList();
        }

        // 4. SAVE (CREATE/EDIT) 
       [HttpPost]
        public async Task<IActionResult> SavePit(int? id, string name, string alias, string division, string department, string location, string status)
        {
            
            int currentId = id ?? 0; 

            // 1. Cek Nama Official Inputan
            var nameConflict1 = await _context.Pits.AnyAsync(p => p.PitNameOfficial == name && p.PitId != currentId);
            if (nameConflict1) return BadRequest($"GAGAL: Nama '{name}' sudah dipakai sebagai Nama Official Pit lain!");

            // B. Apakah Nama ini sudah dipakai sebagai ALIAS di pit lain?
            var nameConflict2 = await _context.PitAliases.AnyAsync(a => a.AliasName == name && a.PitId != currentId);
            if (nameConflict2) return BadRequest($"GAGAL: Nama '{name}' sudah dipakai sebagai Alias di Pit lain!");

            // 2. Siapkan List Alias Inputan
            var inputAliases = string.IsNullOrEmpty(alias) 
                ? new List<string>() 
                : alias.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(a => a.Trim())
                        .Where(a => !string.IsNullOrWhiteSpace(a))
                        .Distinct()
                        .ToList();

            // 3. Cek Setiap Alias Inputan
            foreach (var aliasItem in inputAliases)
            {
                // C. Apakah Alias ini sudah dipakai sebagai ALIAS di pit lain?
                var aliasConflict1 = await _context.PitAliases.AnyAsync(a => a.AliasName == aliasItem && a.PitId != currentId);
                if (aliasConflict1) return BadRequest($"GAGAL: Alias '{aliasItem}' sudah dipakai sebagai Alias di Pit lain!");

                // D. Apakah Alias ini sudah dipakai sebagai NAMA OFFICIAL di pit lain?
                var aliasConflict2 = await _context.Pits.AnyAsync(p => p.PitNameOfficial == aliasItem && p.PitId != currentId);
                if (aliasConflict2) return BadRequest($"GAGAL: Alias '{aliasItem}' sebenarnya adalah Nama Official dari Pit lain! Jangan duplikat.");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try 
            {
                string statusUpper = (status ?? "INACTIVE").ToUpper();

                // Handle Master Data
                var loc = await _context.Locations.FirstOrDefaultAsync(l => l.LocationName == location);
                if (loc == null) { loc = new Location { LocationName = location }; _context.Locations.Add(loc); await _context.SaveChangesAsync(); }
                
                var div = await _context.Divisions.FirstOrDefaultAsync(d => d.DivisionName == division);
                if (div == null) { div = new Division { DivisionName = division }; _context.Divisions.Add(div); await _context.SaveChangesAsync(); }
                
                var dept = await _context.Departments.FirstOrDefaultAsync(d => d.DepartmentName == department && d.DivisionId == div.DivisionId);
                if (dept == null) { dept = new Department { DepartmentName = department, DivisionId = div.DivisionId }; _context.Departments.Add(dept); await _context.SaveChangesAsync(); }

                Pit pitData;
                string actionType = "";
                string msg = "";

                if (id == null || id == 0) {
                    string nextCode = await GenerateNextPitCode();
                    pitData = new Pit { 
                        PitCode = nextCode.ToLower(), 
                        PitNameOfficial = name, 
                        LocationId = loc.LocationId, 
                        DepartmentId = dept.DepartmentId, 
                        Status = statusUpper, 
                        EffectiveDate = DateOnly.FromDateTime(DateTime.Now) 
                    };
                    _context.Pits.Add(pitData); 
                    await _context.SaveChangesAsync(); 
                    actionType = "CREATE";
                    msg = $"Added New Pit: {name} (Code: {pitData.PitCode})"; 
                } else {
                    pitData = await _context.Pits.FirstOrDefaultAsync(p => p.PitId == id);
                    if (pitData == null) return NotFound("Data not found");
                    
                    pitData.PitNameOfficial = name; 
                    pitData.LocationId = loc.LocationId; 
                    pitData.DepartmentId = dept.DepartmentId; 
                    pitData.Status = statusUpper;
                    _context.Pits.Update(pitData); 
                    await _context.SaveChangesAsync();
                    actionType = "UPDATE";
                    msg = $"Updated Pit Information: {name}"; 
                }

                // Handle Alias CRUD
                var existingAliases = await _context.PitAliases.Where(a => a.PitId == pitData.PitId).ToListAsync();
                
                var aliasesToDelete = existingAliases.Where(db => !inputAliases.Any(inp => inp.Equals(db.AliasName, StringComparison.OrdinalIgnoreCase))).ToList();
                if (aliasesToDelete.Any()) _context.PitAliases.RemoveRange(aliasesToDelete);

                foreach (var inputName in inputAliases)
                {
                    var existingItem = existingAliases.FirstOrDefault(db => db.AliasName.Equals(inputName, StringComparison.OrdinalIgnoreCase));
                    if (existingItem != null) {
                        if (existingItem.AliasName != inputName) { 
                            existingItem.AliasName = inputName;
                            _context.Entry(existingItem).State = EntityState.Modified;
                        }
                    } else {
                        _context.PitAliases.Add(new PitAlias { PitId = pitData.PitId, AliasName = inputName });
                    }
                }
                
                await _context.SaveChangesAsync();
                await RegisterActivity(actionType, msg); 
                await transaction.CommitAsync();
                
                return Ok(new { success = true });
            }
            catch (Exception ex) { 
                await transaction.RollbackAsync(); 
                return StatusCode(500, ex.Message); 
            }
        }

        // 5. DELETE 
        [HttpPost]
        public async Task<IActionResult> DeletePit(int id)
        {
            var pit = await _context.Pits.FindAsync(id);
            if (pit != null) {
                string name = pit.PitNameOfficial;
                var aliases = _context.PitAliases.Where(a => a.PitId == id);
                _context.PitAliases.RemoveRange(aliases); _context.Pits.Remove(pit); await _context.SaveChangesAsync();
                

                await RegisterActivity("DELETE", $"Deleted Pit: {name}");
                
                return Ok(new { success = true });
            }
            return BadRequest("Data not found");
        }

        [HttpGet]
        public async Task<JsonResult> GetDepartments(string divisionName)
        {
            if (string.IsNullOrEmpty(divisionName)) return Json(new List<object>());
            var div = await _context.Divisions.FirstOrDefaultAsync(d => d.DivisionName == divisionName);
            if (div == null) return Json(new List<object>());
            return Json(await _context.Departments.Where(d => d.DivisionId == div.DivisionId).Select(d => new { name = d.DepartmentName }).ToListAsync());
        }


        // 1. MANAGE DIVISION
        [HttpPost]
        public async Task<IActionResult> SaveMasterDivision(int? id, string name)
        {
            if (string.IsNullOrEmpty(name)) return BadRequest("Name required");
            
            if (id == null || id == 0) {
                _context.Divisions.Add(new Division { DivisionName = name });
                await RegisterActivity("CREATE", $"Added New Division: {name}");
            } else {
                var item = await _context.Divisions.FindAsync(id);
                if (item != null) { item.DivisionName = name; await RegisterActivity("UPDATE", $"Updated Division: {name}"); }
            }
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> DeleteMasterDivision(int id)
        {
            var item = await _context.Divisions.FindAsync(id);
            if (item != null) {
                _context.Divisions.Remove(item);
                await RegisterActivity("DELETE", $"Deleted Division: {item.DivisionName}");
                await _context.SaveChangesAsync();
                return Ok();
            }
            return NotFound();
        }

        // 2. MANAGE LOCATION
        [HttpPost]
        public async Task<IActionResult> SaveMasterLocation(int? id, string name)
        {
            if (string.IsNullOrEmpty(name)) return BadRequest("Name required");

            if (id == null || id == 0) {
                _context.Locations.Add(new Location { LocationName = name });
                await RegisterActivity("CREATE", $"Added New Location: {name}");
            } else {
                var item = await _context.Locations.FindAsync(id);
                if (item != null) { item.LocationName = name; await RegisterActivity("UPDATE", $"Updated Location: {name}"); }
            }
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> DeleteMasterLocation(int id)
        {
            var item = await _context.Locations.FindAsync(id);
            if (item != null) {
                _context.Locations.Remove(item);
                await RegisterActivity("DELETE", $"Deleted Location: {item.LocationName}");
                await _context.SaveChangesAsync();
                return Ok();
            }
            return NotFound();
        }

        // 3. MANAGE DEPARTMENT
        [HttpPost]
        public async Task<IActionResult> SaveMasterDepartment(int? id, string name, string divisionName) 
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(divisionName)) return BadRequest("Data required");

            var div = await _context.Divisions.FirstOrDefaultAsync(d => d.DivisionName == divisionName);
            if (div == null) return BadRequest("Division not found");

            if (id == null || id == 0) {
                _context.Departments.Add(new Department { DepartmentName = name, DivisionId = div.DivisionId });
                await RegisterActivity("CREATE", $"Added New Dept: {name} to {divisionName}");
            } else {
                var item = await _context.Departments.FindAsync(id);
                if (item != null) { 
                    item.DepartmentName = name; 
                    item.DivisionId = div.DivisionId;
                    await RegisterActivity("UPDATE", $"Updated Dept: {name}"); 
                }
            }
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> DeleteMasterDepartment(int id)
        {
            var item = await _context.Departments.FindAsync(id);
            if (item != null) {
                _context.Departments.Remove(item);
                await RegisterActivity("DELETE", $"Deleted Dept: {item.DepartmentName}");
                await _context.SaveChangesAsync();
                return Ok();
            }
            return NotFound();
        }

        private async Task<string> GenerateNextPitCode()
        {
            var lastPit = await _context.Pits.OrderByDescending(p => p.PitCode).FirstOrDefaultAsync();
            if (lastPit == null) return "A";
            string lastCode = lastPit.PitCode.ToUpper();
            char lastChar = lastCode[lastCode.Length - 1];
            if (lastChar >= 'A' && lastChar < 'Z') return ((char)(lastChar + 1)).ToString();
            else if (lastChar == 'Z') return "AA";
            return "A";
        }

        
    }
}