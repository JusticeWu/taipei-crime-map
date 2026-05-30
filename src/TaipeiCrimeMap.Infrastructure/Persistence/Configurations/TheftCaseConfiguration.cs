using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaipeiCrimeMap.Domain.Aggregates;

namespace TaipeiCrimeMap.Infrastructure.Persistence.Configurations;

public class TheftCaseConfiguration : IEntityTypeConfiguration<TheftCase>
{
    public void Configure(EntityTypeBuilder<TheftCase> builder)
    {
        builder.ToTable("theft_cases");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.CaseNumber)
            .HasColumnName("case_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.CaseType)
            .HasColumnName("case_type")
            .HasConversion<int>();

        builder.Property(x => x.RawLocation)
            .HasColumnName("raw_location")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.ImportedAt)
            .HasColumnName("imported_at");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at");

        // TaiwanDate（Value Object）
        builder.OwnsOne(x => x.OccurredDate, nav =>
        {
            nav.Property(d => d.RawValue)
                .HasColumnName("occurred_date_raw")
                .HasMaxLength(7);

            nav.Property(d => d.OccurredOn)
                .HasColumnName("occurred_date");

            nav.Property(d => d.Year)
                .HasColumnName("occurred_year");
        });

        // TimeSlot（Value Object）
        builder.OwnsOne(x => x.TimeSlot, nav =>
        {
            nav.Property(t => t.RawValue)
                .HasColumnName("time_slot_raw")
                .HasMaxLength(20);

            nav.Property(t => t.StartHour)
                .HasColumnName("time_slot_start");

            nav.Property(t => t.EndHour)
                .HasColumnName("time_slot_end");
        });

        // District（Value Object）
        builder.OwnsOne(x => x.District, nav =>
        {
            nav.Property(d => d.Name)
                .HasColumnName("district")
                .HasMaxLength(10);
        });

        // GeoCoordinate（Value Object）
        builder.OwnsOne(x => x.Coordinate, nav =>
        {
            nav.Property(c => c.Latitude)
                .HasColumnName("latitude");

            nav.Property(c => c.Longitude)
                .HasColumnName("longitude");
        });

        // Domain Events 不持久化
        builder.Ignore(x => x.DomainEvents);

        // IsDataComplete 是計算屬性，不對應欄位
        builder.Ignore(x => x.IsDataComplete);

        builder.HasIndex(x => x.CaseNumber)
            .IsUnique();
    }
}