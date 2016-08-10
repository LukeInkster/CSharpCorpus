// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.EntityFrameworkCore.Specification.Tests.TestModels
{
    public class SnapshotMonsterContext : MonsterContext<
        SnapshotMonsterContext.Customer, SnapshotMonsterContext.Barcode, SnapshotMonsterContext.IncorrectScan,
        SnapshotMonsterContext.BarcodeDetail, SnapshotMonsterContext.Complaint, SnapshotMonsterContext.Resolution,
        SnapshotMonsterContext.Login, SnapshotMonsterContext.SuspiciousActivity, SnapshotMonsterContext.SmartCard,
        SnapshotMonsterContext.RsaToken, SnapshotMonsterContext.PasswordReset, SnapshotMonsterContext.PageView,
        SnapshotMonsterContext.LastLogin, SnapshotMonsterContext.Message, SnapshotMonsterContext.AnOrder,
        SnapshotMonsterContext.OrderNote, SnapshotMonsterContext.OrderQualityCheck, SnapshotMonsterContext.OrderLine,
        SnapshotMonsterContext.Product, SnapshotMonsterContext.ProductDetail, SnapshotMonsterContext.ProductReview,
        SnapshotMonsterContext.ProductPhoto, SnapshotMonsterContext.ProductWebFeature, SnapshotMonsterContext.Supplier,
        SnapshotMonsterContext.SupplierLogo, SnapshotMonsterContext.SupplierInfo, SnapshotMonsterContext.CustomerInfo,
        SnapshotMonsterContext.Computer, SnapshotMonsterContext.ComputerDetail, SnapshotMonsterContext.Driver,
        SnapshotMonsterContext.License>
    {
        public SnapshotMonsterContext(DbContextOptions options, Action<ModelBuilder> onModelCreating)
            : base(options, onModelCreating)
        {
        }

        // TODO: Inheritance
        //public class BackOrderLine2 : BackOrderLine
        //{
        //}

        //public class BackOrderLine : OrderLine
        //{
        //    public DateTime ETA { get; set; }

        //    public int SupplierId { get; set; }
        //    public virtual ISupplier Supplier { get; set; }
        //}

        public class BarcodeDetail : IBarcodeDetail
        {
            public byte[] Code { get; set; }
            public string RegisteredTo { get; set; }
        }

        public class Barcode : IBarcode
        {
            public void InitializeCollections()
            {
                BadScans = BadScans ?? new HashSet<IIncorrectScan>();
            }

            public byte[] Code { get; set; }
            public int ProductId { get; set; }
            public string Text { get; set; }

            public virtual IProduct Product { get; set; }
            public virtual ICollection<IIncorrectScan> BadScans { get; set; }
            public virtual IBarcodeDetail Detail { get; set; }
        }

        public class Complaint : IComplaint
        {
            public int ComplaintId { get; set; }
            public int AlternateId { get; set; }
            public int? CustomerId { get; set; }
            public DateTime Logged { get; set; }
            public string Details { get; set; }

            public virtual ICustomer Customer { get; set; }
            public virtual IResolution Resolution { get; set; }
        }

        public class ComputerDetail : IComputerDetail
        {
            public ComputerDetail()
            {
                Dimensions = new Dimensions();
            }

            public int ComputerDetailId { get; set; }
            public string Manufacturer { get; set; }
            public string Model { get; set; }
            public string Serial { get; set; }
            public string Specifications { get; set; }
            public DateTime PurchaseDate { get; set; }

            public Dimensions Dimensions { get; set; }

            public virtual IComputer Computer { get; set; }
        }

        public class Computer : IComputer
        {
            public int ComputerId { get; set; }
            public string Name { get; set; }

            public virtual IComputerDetail ComputerDetail { get; set; }
        }

        public class CustomerInfo : ICustomerInfo
        {
            public int CustomerInfoId { get; set; }
            public string Information { get; set; }
        }

        //public class DiscontinuedProduct : Product
        //{
        //    public DateTime Discontinued { get; set; }
        //    public int? ReplacementProductId { get; set; }

        //    public virtual IProduct ReplacedBy { get; set; }
        //}

        public class Driver : IDriver
        {
            public string Name { get; set; }
            public DateTime BirthDate { get; set; }

            public virtual ILicense License { get; set; }
        }

        public class IncorrectScan : IIncorrectScan
        {
            public int IncorrectScanId { get; set; }
            public byte[] ExpectedCode { get; set; }
            public byte[] ActualCode { get; set; }
            public DateTime ScanDate { get; set; }
            public string Details { get; set; }

            public virtual IBarcode ExpectedBarcode { get; set; }
            public virtual IBarcode ActualBarcode { get; set; }
        }

        public class LastLogin : ILastLogin
        {
            public string Username { get; set; }
            public DateTime LoggedIn { get; set; }
            public DateTime? LoggedOut { get; set; }

            public string SmartcardUsername { get; set; }

            public virtual ILogin Login { get; set; }
        }

        public class License : ILicense
        {
            public License()
            {
                LicenseClass = "C";
            }

            public string Name { get; set; }
            public string LicenseNumber { get; set; }
            public string LicenseClass { get; set; }
            public string Restrictions { get; set; }
            public DateTime ExpirationDate { get; set; }
            public LicenseState? State { get; set; }

            public virtual IDriver Driver { get; set; }
        }

        public class Message : IMessage
        {
            public int MessageId { get; set; }
            public string FromUsername { get; set; }
            public string ToUsername { get; set; }
            public DateTime Sent { get; set; }
            public string Subject { get; set; }
            public string Body { get; set; }
            public bool IsRead { get; set; }

            public virtual ILogin Sender { get; set; }
            public virtual ILogin Recipient { get; set; }
        }

        public class OrderLine : IOrderLine
        {
            public OrderLine()
            {
                Quantity = 1;
            }

            public int OrderId { get; set; }
            public int ProductId { get; set; }
            public int Quantity { get; set; }
            public string ConcurrencyToken { get; set; }

            public virtual IAnOrder Order { get; set; }
            public virtual IProduct Product { get; set; }
        }

        public class AnOrder : IAnOrder
        {
            public AnOrder()
            {
                Concurrency = new ConcurrencyInfo();
            }

            public void InitializeCollections()
            {
                OrderLines = OrderLines ?? new HashSet<IOrderLine>();
                Notes = Notes ?? new HashSet<IOrderNote>();
            }

            public int AnOrderId { get; set; }
            public int AlternateId { get; set; }
            public int? CustomerId { get; set; }

            public ConcurrencyInfo Concurrency { get; set; }

            public virtual ICustomer Customer { get; set; }
            public virtual ICollection<IOrderLine> OrderLines { get; set; }
            public virtual ICollection<IOrderNote> Notes { get; set; }

            public string Username { get; set; }
            public virtual ILogin Login { get; set; }
        }

        public class OrderNote : IOrderNote
        {
            public int NoteId { get; set; }
            public string Note { get; set; }

            public int OrderId { get; set; }
            public virtual IAnOrder Order { get; set; }
        }

        public class OrderQualityCheck : IOrderQualityCheck
        {
            public int OrderId { get; set; }
            public string CheckedBy { get; set; }
            public DateTime CheckedDateTime { get; set; }

            public virtual IAnOrder Order { get; set; }
        }

        public class PageView : IPageView
        {
            public int PageViewId { get; set; }
            public string Username { get; set; }
            public DateTime Viewed { get; set; }
            public string PageUrl { get; set; }

            public virtual ILogin Login { get; set; }
        }

        public class PasswordReset : IPasswordReset
        {
            public int ResetNo { get; set; }
            public string Username { get; set; }
            public string TempPassword { get; set; }
            public string EmailedTo { get; set; }

            public virtual ILogin Login { get; set; }
        }

        public class ProductDetail : IProductDetail
        {
            public int ProductId { get; set; }
            public string Details { get; set; }

            public virtual IProduct Product { get; set; }
        }

        public class Product : IProduct
        {
            public Product()
            {
                Dimensions = new Dimensions();
                ComplexConcurrency = new ConcurrencyInfo();
                NestedComplexConcurrency = new AuditInfo();
            }

            public void InitializeCollections()
            {
                Suppliers = Suppliers ?? new HashSet<ISupplier>();
                //Replaces = Replaces ?? new HashSet<DiscontinuedProduct>();
                Reviews = Reviews ?? new HashSet<IProductReview>();
                Photos = Photos ?? new HashSet<IProductPhoto>();
                Barcodes = Barcodes ?? new HashSet<IBarcode>();
            }

            public int ProductId { get; set; }
            public string Description { get; set; }
            public string BaseConcurrency { get; set; }

            public Dimensions Dimensions { get; set; }
            public ConcurrencyInfo ComplexConcurrency { get; set; }
            public AuditInfo NestedComplexConcurrency { get; set; }

            public virtual ICollection<ISupplier> Suppliers { get; set; }
            //public virtual ICollection<DiscontinuedProduct> Replaces { get; set; }
            public virtual IProductDetail Detail { get; set; }
            public virtual ICollection<IProductReview> Reviews { get; set; }
            public virtual ICollection<IProductPhoto> Photos { get; set; }
            public virtual ICollection<IBarcode> Barcodes { get; set; }
        }

        public class ProductPageView : PageView
        {
            public int ProductId { get; set; }

            public virtual IProduct Product { get; set; }
        }

        public class ProductPhoto : IProductPhoto
        {
            public void InitializeCollections()
            {
                Features = Features ?? new HashSet<IProductWebFeature>();
            }

            public int ProductId { get; set; }
            public int PhotoId { get; set; }
            public byte[] Photo { get; set; }

            public virtual ICollection<IProductWebFeature> Features { get; set; }
        }

        public class ProductReview : IProductReview
        {
            public void InitializeCollections()
            {
                Features = Features ?? new HashSet<IProductWebFeature>();
            }

            public int ProductId { get; set; }
            public int ReviewId { get; set; }
            public string Review { get; set; }

            public virtual IProduct Product { get; set; }
            public virtual ICollection<IProductWebFeature> Features { get; set; }
        }

        public class ProductWebFeature : IProductWebFeature
        {
            public int FeatureId { get; set; }
            public int? ProductId { get; set; }
            public int? PhotoId { get; set; }
            public int ReviewId { get; set; }
            public string Heading { get; set; }

            public virtual IProductReview Review { get; set; }
            public virtual IProductPhoto Photo { get; set; }
        }

        public class Resolution : IResolution
        {
            public int ResolutionId { get; set; }
            public string Details { get; set; }

            public virtual IComplaint Complaint { get; set; }
        }

        public class RsaToken : IRsaToken
        {
            public string Serial { get; set; }
            public DateTime Issued { get; set; }

            public string Username { get; set; }
            public virtual ILogin Login { get; set; }
        }

        public class SmartCard : ISmartCard
        {
            public string Username { get; set; }
            public string CardSerial { get; set; }
            public DateTime Issued { get; set; }

            public virtual ILogin Login { get; set; }
            public virtual ILastLogin LastLogin { get; set; }
        }

        public class SupplierInfo : ISupplierInfo
        {
            public int SupplierInfoId { get; set; }
            public string Information { get; set; }

            public int SupplierId { get; set; }
            public virtual ISupplier Supplier { get; set; }
        }

        public class SupplierLogo : ISupplierLogo
        {
            public int SupplierId { get; set; }
            public byte[] Logo { get; set; }
        }

        public class Supplier : ISupplier
        {
            public void InitializeCollections()
            {
                Products = Products ?? new HashSet<IProduct>();
                //BackOrderLines = new HashSet<BackOrderLine>();
            }

            public int SupplierId { get; set; }
            public string Name { get; set; }

            public virtual ICollection<IProduct> Products { get; set; }
            //public virtual ICollection<BackOrderLine> BackOrderLines { get; set; }
            public virtual ISupplierLogo Logo { get; set; }
        }

        public class SuspiciousActivity : ISuspiciousActivity
        {
            public int SuspiciousActivityId { get; set; }
            public string Activity { get; set; }

            public string Username { get; set; }
        }

        public class Customer : ICustomer
        {
            public Customer()
            {
                ContactInfo = new ContactDetails();
                Auditing = new AuditInfo();
            }

            public void InitializeCollections()
            {
                Orders = Orders ?? new HashSet<IAnOrder>();
                Logins = Logins ?? new HashSet<ILogin>();
            }

            public int CustomerId { get; set; }
            public int? HusbandId { get; set; }
            public string Name { get; set; }

            public ContactDetails ContactInfo { get; set; }
            public AuditInfo Auditing { get; set; }

            public virtual ICollection<IAnOrder> Orders { get; set; }
            public virtual ICollection<ILogin> Logins { get; set; }
            public virtual ICustomer Husband { get; set; }
            public virtual ICustomer Wife { get; set; }
            public virtual ICustomerInfo Info { get; set; }
        }

        public class Login : ILogin
        {
            public void InitializeCollections()
            {
                SentMessages = SentMessages ?? new HashSet<IMessage>();
                ReceivedMessages = ReceivedMessages ?? new HashSet<IMessage>();
                Orders = Orders ?? new HashSet<IAnOrder>();
            }

            public string Username { get; set; }
            public string AlternateUsername { get; set; }
            public int CustomerId { get; set; }

            public virtual ICustomer Customer { get; set; }
            public virtual ILastLogin LastLogin { get; set; }
            public virtual ICollection<IMessage> SentMessages { get; set; }
            public virtual ICollection<IMessage> ReceivedMessages { get; set; }
            public virtual ICollection<IAnOrder> Orders { get; set; }
        }
    }
}
