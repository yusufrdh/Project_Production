using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using KP_InternalSystem.Models; 

namespace KP_InternalSystem.Data 
{
    public partial class RatDbContext : DbContext
    {
        public RatDbContext() { }

        public RatDbContext(DbContextOptions<RatDbContext> options)
            : base(options) { }

        // --- DAFTAR TABEL ---
        public virtual DbSet<Department> Departments { get; set; }
        public virtual DbSet<Division> Divisions { get; set; }
        public virtual DbSet<Location> Locations { get; set; }
        public virtual DbSet<Pit> Pits { get; set; }
        public virtual DbSet<PitAlias> PitAliases { get; set; }
        public virtual DbSet<UserActivityLog> UserActivityLogs { get; set; }
        

        // --- TABLE PRODUCT BARU ---
        public virtual DbSet<Product> Products { get; set; }
        public virtual DbSet<ProductAlias> ProductAliases { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer("Server=localhost;Database=db_production;Trusted_Connection=True;TrustServerCertificate=True;");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
                        
            modelBuilder.Entity<Product>().ToTable("coal");
            modelBuilder.Entity<ProductAlias>().ToTable("coal_alias");
           // modelBuilder.Entity<FactActual>().ToTable("cm_actual");
           // modelBuilder.Entity<FactPlan>().ToTable("cm_plan");
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}