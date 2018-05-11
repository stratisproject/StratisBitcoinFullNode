### Code Review Policy

#### How to review pull requests  

This is a guide for core developers and contributors who want to help with code reviewing PRs on the fullnode.  
We welcome anyone to help with code reviews, from typos to suggestions in design.   

#### Why are code reviews important?  
It's essential that PRs are properly code-reviewed.   
Changes to the code, even small changes as incrementing an integer, can result in critical consensus bugs and loss of funds if not reviewed properly.  
Some PRs are more severe than others in importance (for example changes to tests may not require the same level of checks as changes to the consensus code).  
For that reason we have a PR review policy with 3 tags. A PR must be associated with a PR review policy.

#### Requirements

The requirement for each PR policy

*pr-review-critical*
- Code must be reviewed by at least two core devs
- All tests must pass (Unit-test and Integration-tests) (run by auther and the two reviewers)
- All 4 chains (Bitcoin main/test, Stratis main/test) need to have been synced (to an agreed height) (run by auther and 2 reviewers)

*pr-review-high*
- Code must be reviewed by at least two core devs
- All tests must pass (Unit-test and Integration-tests) (run by auther and the two reviewers)
- All 4 chains (Bitcoin main/test, Stratis main/test) need to have been synced (to an agreed height)

*pr-review-medium*
- Code must be reviewed by at least two core dev (run by auther and reviewr)
- All tests must pass (Unit-test and Integration-tests) 

*pr-review-low*
- Code must be reviewed by at least one core dev
- Unit-test must pass (automated CI tests)


#### What to look for in code reviews

* **Correctness in business logic**: check whether the change makes sense from a pure business logic perspective.
* **Changes in interface/architectural design**: a mistake here may lead to something that previously worked may not work anymore.
* **Code robustness**: check whether the new code is solid and defensive, i.e checks for nullity.
* **Tests**: make sure tests have been added to support the code change. If not, figure out why.
* **Code styling**: make sure our [code style policy](https://github.com/stratisproject/StratisBitcoinFullNode/blob/master/Documentation/coding-style.md) is respected.
* **Typos and grammar**: make sure the code reads smoothly and is in British English. 
* **Issue reference**: check whether the PR references an issue and make sure both are linked.
