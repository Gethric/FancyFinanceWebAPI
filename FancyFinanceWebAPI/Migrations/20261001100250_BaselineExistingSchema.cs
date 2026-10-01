using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FancyFinanceWebAPI.Migrations
{
    /// <inheritdoc />
    public partial class BaselineExistingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The tables already exist in Supabase.
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Leave the existing tables intact.
        }
    }
}
