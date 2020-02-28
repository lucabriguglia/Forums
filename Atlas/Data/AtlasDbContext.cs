﻿using System.Reflection;
using Atlas.Models;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Data
{
    public class AtlasDbContext : DbContext
    {
        public AtlasDbContext(DbContextOptions<AtlasDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            builder.Entity<Member>()
                .ToTable("Member");

            builder.Entity<PermissionSet>()
                .ToTable("PermissionSet");

            builder.Entity<Permission>()
                .ToTable("Permission")
                .HasKey(x => new { x.PermissionSetId, x.RoleId, x.Type });

            builder.Entity<Reply>()
                .ToTable("Reply");
        }

        public DbSet<Forum> Forums { get; set; }
        public DbSet<ForumGroup> ForumGroups { get; set; }
        public DbSet<Member> Members { get; set; }
        public DbSet<PermissionSet> PermissionSets { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<Reply> Replies { get; set; }
        public DbSet<Site> Sites { get; set; }
        public DbSet<Topic> Topics { get; set; }
    }
}
