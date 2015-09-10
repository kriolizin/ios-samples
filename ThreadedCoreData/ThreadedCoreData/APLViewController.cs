// This file has been autogenerated from a class added in the UI designer.
using System;
using CoreData;
using Foundation;
using ObjCRuntime;
using UIKit;

namespace ThreadedCoreData
{
	public partial class APLViewController : UITableViewController
	{
		NSManagedObjectContext managedObjectContext;
		NSFetchedResultsController fetchedResultsController;
		FecthResultsControllerDelegate fecthResultsControllerDelegate;
		NSPersistentStoreCoordinator persistentStoreCoordinator;
		NSManagedObjectModel managedObjectModel;
		UIBarButtonItem activityIndicator;
		NSOperationQueue parseQueue;

		string ApplicationDocumentsDirectory {
			get {
				string[] directories = NSSearchPath.GetDirectories (NSSearchPathDirectory.DocumentDirectory,
					NSSearchPathDomain.User, true);
				return directories.Length != 0 ? directories [directories.Length - 1] : string.Empty;
			}
		}

		NSManagedObjectModel ManagedObjectModel {
			get {
				if (managedObjectModel != null)
					return managedObjectModel;

				string path = NSBundle.PathForResourceAbsolute ("Earthquakes", "mom", NSBundle.MainBundle.ResourcePath);
				var modelUrl = NSUrl.FromFilename (path);
				managedObjectModel = new NSManagedObjectModel (modelUrl);
				return managedObjectModel;
			}
		}

		NSManagedObjectContext ManagedObjectContext {
			get {
				if (managedObjectContext != null)
					return managedObjectContext;

				managedObjectContext = new NSManagedObjectContext () {
					PersistentStoreCoordinator = PersistentStoreCoordinator
				};

				NSNotificationCenter.DefaultCenter.AddObserver (this, new Selector ("MergeChanges:"),
				                                                NSManagedObjectContext.DidSaveNotification, null);

				return managedObjectContext;
			}
		}

		NSPersistentStoreCoordinator PersistentStoreCoordinator {
			get {
				if (persistentStoreCoordinator != null)
					return persistentStoreCoordinator;

				string storePath = ApplicationDocumentsDirectory + "/Earthquakes.sqlite";
				var storeUrl = NSUrl.FromFilename (storePath);

				persistentStoreCoordinator = new NSPersistentStoreCoordinator (ManagedObjectModel);

				NSError error;
				persistentStoreCoordinator.AddPersistentStoreWithType (NSPersistentStoreCoordinator.SQLiteStoreType,
				                                                       null, storeUrl, null, out error);
				if (error != null)
					Console.WriteLine (string.Format ("Unresolved error {0}", error.LocalizedDescription));

				return persistentStoreCoordinator;
			}
		}

		NSFetchedResultsController FetchedResultsController {
			get {
				if (fetchedResultsController == null) {
					var fetchRequest = new NSFetchRequest ();
					var entity = NSEntityDescription.EntityForName ("Earthquake", ManagedObjectContext);
					fetchRequest.Entity = entity;

					fetchRequest.SortDescriptors = new  NSSortDescriptor[] { new NSSortDescriptor ("date", false) };

					fetchedResultsController = new NSFetchedResultsController (fetchRequest, ManagedObjectContext, null, null);
					fetchedResultsController.Delegate = fecthResultsControllerDelegate;

					NSError error;
					if (fetchedResultsController.PerformFetch (out error)) {
						if (error != null)
							Console.WriteLine (string.Format ("Unresolved error {0}", error.LocalizedDescription));
					}
				}

				return fetchedResultsController;
			}
		}

		public APLViewController (IntPtr handle) : base (handle)
		{
		}

		public override void ViewDidLoad ()
		{
			base.ViewDidLoad ();

			fecthResultsControllerDelegate = new FecthResultsControllerDelegate () {
				TableView = TableView
			};

			var feedURLString = new NSString ("http://earthquake.usgs.gov/earthquakes/feed/v1.0/summary/4.5_week.geojson");
			var earthquakeURLRequest = NSUrlRequest.FromUrl (new NSUrl (feedURLString));
			NSUrlConnection.SendAsynchronousRequest (earthquakeURLRequest, NSOperationQueue.MainQueue, RequestCompletionHandler);

			UIApplication.SharedApplication.NetworkActivityIndicatorVisible = true;
			parseQueue = new NSOperationQueue ();
			parseQueue.AddObserver (this, new NSString ("operationCount"), NSKeyValueObservingOptions.New, IntPtr.Zero);

			//HACK: Parsed strings to NSString
			NSNotificationCenter.DefaultCenter.AddObserver (this, new Selector ("EarthquakesError:"), (NSString)APLParseOperation.EarthquakesErrorNotificationName, null);
			NSNotificationCenter.DefaultCenter.AddObserver (this, new Selector ("LocaleChanged:"), (NSString)NSLocale.CurrentLocaleDidChangeNotification, null);

			var spinner = new UIActivityIndicatorView (UIActivityIndicatorViewStyle.White);
			spinner.StartAnimating ();
			activityIndicator = new UIBarButtonItem (spinner);
			NavigationItem.RightBarButtonItem = activityIndicator;
		}

		public override void ObserveValue (NSString keyPath, NSObject ofObject, NSDictionary change, IntPtr context)
		{
			if (ofObject == parseQueue && keyPath == "operationCount") {
				if (parseQueue.OperationCount == 0)
					InvokeOnMainThread (new Selector ("HideActivityIndicator"), null);
			} else {
				base.ObserveValue (keyPath, ofObject, change, context);
			}
		}

		public override nint NumberOfSections (UITableView tableView)
		{
			return FetchedResultsController.Sections.Length;
		}

		public override nint RowsInSection (UITableView tableview, nint section)
		{
			int numberOfRows = 0;

			// HACK: Parsed the Count to int
			if (FetchedResultsController.Sections.Length > 0)
				numberOfRows = (int)FetchedResultsController.Sections [section].Count;

			return numberOfRows;
		}

		public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
		{
			var cell = (APLEarthquakeTableViewCell)tableView.DequeueReusableCell (APLEarthquakeTableViewCell.Key);
			cell.SelectionStyle = UITableViewCellSelectionStyle.None;
			var earthquake = new ManagedEarthquake (FetchedResultsController.ObjectAt (indexPath).Handle);
			cell.ConfigureWithEarthquake (earthquake);
			return cell;
		}

		[Export("EarthquakesError:")]
		public void EarthquakesError (NSNotification notification)
		{
			if (notification.Name == APLParseOperation.EarthquakesErrorNotificationName)
				HandleError ((NSError)notification.UserInfo.ValueForKey (new NSString (APLParseOperation.EarthquakesMessageErrorKey)));
		}

		[Export("MergeChanges:")]
		public void MergeChanges (NSNotification notification)
		{
			if (notification.Object != ManagedObjectContext)
				InvokeOnMainThread (new Selector ("UpdateMainContext:"), notification);
		}

		[Export("LocaleChanged:")]
		public void LocaleChanged (NSNotification notification)
		{
			TableView.ReloadData ();
		}

		[Export("UpdateMainContext:")]
		public void UpdateMainContext (NSNotification notification)
		{
			ManagedObjectContext.MergeChangesFromContextDidSaveNotification (notification);
		}

		[Export("HandleError:")]
		public void HandleError (NSError error)
		{
			string errorMessage = error.LocalizedDescription;
			string alertTitle = "Error";
			var alert = new UIAlertView (alertTitle, errorMessage, null, "Ok");
			alert.Show ();
		}

		[Export("HideActivityIndicator")]
		public void HideActivityIndicator ()
		{
			var indicator = (UIActivityIndicatorView)activityIndicator.CustomView;
			indicator.StopAnimating ();
			NavigationItem.RightBarButtonItem = null;
		}

		NSError ComposeError (string message, string domain, int statusCode)
		{
			var errorMessage = new NSString (message);
			var userInfo = new NSDictionary (NSError.LocalizedDescriptionKey, errorMessage);
			return new NSError (new NSString (domain), statusCode, userInfo);
		}

		void RequestCompletionHandler (NSUrlResponse responce, NSData data, NSError error)
		{
			UIApplication.SharedApplication.NetworkActivityIndicatorVisible = false;
			if (error != null) {
				HandleError (error);
			} else {
				var httpResponse = (NSHttpUrlResponse)responce;
				if (httpResponse.StatusCode / 100 == 2 && responce.MimeType == "application/json") {
					var parseOperation = new APLParseOperation (data, PersistentStoreCoordinator);
					parseQueue.AddOperation (parseOperation);
				} else {
					var userInfo = new NSDictionary(NSError.LocalizedDescriptionKey, "Problems with connection.");
					var reportError = new NSError (new NSString ("HTTP"), httpResponse.StatusCode, userInfo);
					HandleError (reportError);
				}
			}
		}

		class FecthResultsControllerDelegate : NSFetchedResultsControllerDelegate
		{
			public UITableView TableView { get; set; }

			public override void DidChangeContent (NSFetchedResultsController controller)
			{
				TableView.ReloadData ();
			}
		}
	}
}
