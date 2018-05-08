### Code Review Policy

#### How to code review pull requests  

This is a guide for core developers and contributors who want to help with code reviewing PRs on the fullnode.  
We welcome anyone to help with code reviews, from types to suggestions in design.   

#### Why are code reviews important?  
It's essential that PRs are properly code-reviewed,   
changes to the code, even small changes as incrementing an integer, can result in critical consensus bugs and lose of funds if not reviewed properly.  
Some PRs are more severe then others in importance (for example changes to tests may not require the same level of checks as changes to the consensus code).  
For that reason we have a PR review policy with 3 tags, A PR must be associated with a pr review policy.

#### Requirements

The requirement for each pr policy

*pr-review-high*
- Code must be review by at least two core devs
- All tests must pass (unit test and Integration tests)
- All 4 chains need to have been synced (to a certain height)


*pr-review-medium*
- Code must be review by at least two core devs
- All tests must pass (unit test and Integration tests) 

*pr-review-low*
- Code must be review by at one core devs
- unit test must pass
