Test Guidelines
===============

Unit Testing
------------
For unit testing we use Xunit and Moq.

Unit test preparations
1. If a class contains multiple classes try to see if you can move them to separate files inside the same folder. Ideally we have 1 file per class.
2. Create an interface for each class. This helps out a lot with testability. These can be inside the same file as the initial class or in a separate file in the same folder.
3. Try to use dependencies by their interface instead of by their concrete type as much as possible.
4. Move dependencies to the constructor so they can be moved into the dependency injection(DI) framework later. Again try to use their interface and not their concrete type.
   For backward compatibility an overload can be created until the DI registration is done.
  
  ```csharp
  public RepositoryUser()
  {
	this.Repository = new Repository();
  }
  ->
  public RepositoryUser() : this(new Repository());
  {	
  }  
  public RepositoryUser(IRepository repository)
  {
	Guard.NotNull(repository, nameof(repository));
	
	this.Repository = repository;
  }  
  ```
General project rules:
1. The unit test project has to be named {ProjectName}.Tests
2. The test file should be located at the same place in the test project. 
   For example if have file located in **Stratis.Bitcoin\BlockStore\BlockRepository.cs** 
   it should be **Stratis.Bitcoin.Test\BlockStore\BlockRepositoryTest.cs** in the test project.
3. As a file naming convention {ClassName}Test.cs

General testing rules:
1. Focus on testing only a single method at a time.
2. In you tests remove the dependencies from the class under test by using mocks. For more information regarding mocks look [HERE](https://github.com/Moq/moq4/wiki/Quickstart)
3. Do not try to test the entire method in one test. You may need more than 1 test to cover all possible cases.
4. The test method name follows the following structure: {MethodName}{GivenContext}{ExpectedOutcome}. Example for a method called Query: QueryWithoutInitializedRepositoryThrowsException.
5. Test public/Protected/Internal methods. Testing private methods should be an exceptional case.
6. A test must have the following structure(see example below):
	* Setup test context
	* NewLine
	* Call method under test
	* NewLine
	* Assert Result
7. DRY (don't repeat yourself). If you do the same initialization in each test mode that to the test class constructor. That code is then called before every test is called.
8. Test getters and setters only if they contain complex logic that you want to prove with a test.
9. Do not test for null reference exceptions on called methods. Add a Guard.NotNull instead. 
   These are generally coding mistakes that can be taken out when code reviewing and testing this does not add much value.
10. If a class you're trying to test is abstract create a private class inside the test class that inherits from the abstract class.
   Create methods with the new keyword that calls the method on the abstract class and passes on the parameters. This enables you to test the abstract class.
   ```csharp
   public abstract class BaseRepository
   {
		IDbConnection connection;
		
		protected BaseRepository(IDbConnection connection)
		{
			Guard.NotNull(connection, nameof(connection));
		
			this.connection = connection;
		}
   
		protected IDbContext GetContext()
		{
			return this.connection.Context;
		}
   }
   
   public class BaseRepositoryTest
   {
		[Fact]
		public void GetContextReturnsContext()
		{
			var connection = new DbConnection();
			var dbConnectionMock = new Mock<IDbConnection>();
			dbConnectionMock.Setup(d=> d.Context)
				.Returns(connection);			
			
			var repository = new BaseRepositoryStub(dbConnectionMock.Object);
			var result = repository.GetContext();
			
			Assert.Equal(connection, result);
		}
   
		private class BaseRepositoryStub : BaseRepository
		{
			public BaseRepositoryStub(IDbConnection connection): base(connection)
			{
			}
			
			public new IDbContext GetContext()
			{
				return base.GetContext();
			}
		}
   }
   ```