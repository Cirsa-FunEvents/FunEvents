using FunEvents.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FunEvents.Migrations.Migrations;

[DbContext(typeof(FunEventsDbContext))]
[Migration("20261110000000_AddPaymentAttempts")]
partial class AddPaymentAttempts
{
}
