# Ducks

[![Build status](https://ci.appveyor.com/api/projects/status/jeujj9n91gr9t6y7/branch/master?svg=true)](https://ci.appveyor.com/project/busterwood/ducks/branch/master)

[Duck typing](https://en.wikipedia.org/wiki/Duck_typing) of *interfaces* at runtime for .NET.

> if it walks like a duck, and talks like a duck, then its a duck.

`Ducks` allows you dynamically cast an object to an interface, as long as the object has methods/properties that match the interface.
`Ducks` has methods to *duck type* to an interface:
* an instance of a class
* a class's static methods
* a delegate to a single method interface

## Why duck typing?

* loose coupling
* composition at *at run-time* not complie-time
* easier testing by duck-typing static methods (see below)

## Why duck type an instance?

Using "Duck typing" allows assemblies to be loosely coupled, allowing you change compile-time compile-time dependencies to be run-time dependencies.  For example `Ducks` allows you to write code that defines the interface *it wants to consume* but does not require users of your code to directly implement your interface:

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
```

Style note: you may wish to define methods that do duck typing as an extension method, e.g. the first method above could be defined as as an extension method.

## Why static duck typing?

`Ducks` also allows you to use interfaces that are fulfilled by static types, allowing you code to be loosely coupled rather than directly calling static methods.  For example, you no longer need to directly call static methods on the `System.IO.File` class (or `System.IO.File`), you can define an interface for the methods you need:

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

## How do I get my original object back?

When you call `Duck.Cast<>()` you get an instance of a run-time generated type that fulfills all the interfaces of you original object.  To get you original object back, call `Duck.Cast<>()` again with your original object type, for example:

```csharp
public class MyThing {
  void DoStuff();
}

public interface IStuffDoer {
  void DoStuff();
}


...
  var original = new MyThing();
  var duck = Duck.Cast<ISuffDoer>(original); // duck is a run-time generated proxy
  var backAgain = Duck.Cast<MyThing>(duck); // orignal and backAgain now contain a reference to the same object
...
```

### Inspiration

I was inspired by the [interface mechanism of Go](https://golang.org/).
