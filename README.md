# Ducks

[![Build status](https://ci.appveyor.com/api/projects/status/jeujj9n91gr9t6y7/branch/master?svg=true)](https://ci.appveyor.com/project/busterwood/ducks/branch/master)

[Duck typing](https://en.wikipedia.org/wiki/Duck_typing) of *interfaces* at runtime for .NET.

> if it walks like a duck, and talks like a duck, then its a duck.

`Ducks` allows you dynamically cast an object to an interface, as long as the object has methods/properties that match the interface.
`Ducks` has methods to *duck type* to an interface:
* an instance of a class
* a class's static methods
* a delegate to a single method interface

## Why duck type an instance?

Duck typing allows modules to be loosely coupled, for example `Ducks` allows you to write code that defines the interface *it wants to consume*, for example:

```csharp
namespace MyStuff {
	
	public interface IWant {
		void DoStuff();
	}

	public class MyThing {

		// accept any object that implicitly implements the interface
		public void MyMethod(object obj) {
			IWant requires = Duck.Cast<IWant>(obj);	// will throw an InvalidCastExcetpion if obj does not have a 'void DoStuff()'' method
			MyMethod(requires); // call the real method
		}

		// accept any object that explicity implements the interface 
		public void MyMethod(IWant requires) {
			...
			requires.DoStuff();
			...
		}
	}
}
````

## Why static duck typing?

`Ducks` also allows you to use interfaces that are fulfilled by static types, for example you can use this for IO:

```csharp
namespace MyStuff {
	
	public interface IExistDeleter {
		bool Exists(string path);
		void Delete(string path);
	}

	public class MyThing {

		readonly IExistDeleter filesystem;

		// System.IO.File implicitly implements the interface
		public MyThing() {
			filesystem = Duck.Cast<IExistDeleter>(typeof(System.IO.File));
		}

		// accept any object that explicity implements the interface 
		public MyThing(IExistDeleter filesystem) {
			this.filesystem = filesystem;
		}

		public void Delete(string[] paths) {
			foreach (var p in paths) {
				if (filesystem.Exists(path))
					filesystem.Delete(path);
			}
		}
	}
}
```

## Why delegate duck typing?

It is easy to get a delegate for a single method interface, but not the other way around, `Ducks` makes this easy, for example:

```csharp
namespace MyStuff {
	
	public interface IExister {
		bool Exists(string path);
	}

	public class MyThing {

		readonly IExister filesystem;

		// convert the delegate to an instance of the interface
		public MyThing(Func<string, bool> exists) {
			filesystem = Duck.Cast<IExister>(exists);
		}

		// accept any object that explicity implements the interface 
		public MyThing(IExister filesystem) {
			this.filesystem = filesystem;
		}

		public void Delete(string[] paths) {
			foreach (var p in paths) {
				if (filesystem.Exists(path))
					Console.WriteLine($"{path} exists")
			}
		}
	}
}
```
