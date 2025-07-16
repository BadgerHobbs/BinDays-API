# Frequently Asked Questions

Before creating a GitHub issue, read these FAQs.

## General

**Q: What is this?**

A: It's the server-side part of the BinDays project. It provides the logic for getting bin collection times from UK councils.

**Q: How is it different from other projects?**

A: It doesn't scrape council websites from a server. It tells a client app what requests to make, so the requests come from a user's IP address. This avoids IP blocks and means council-specific logic can be updated without users needing to update their app.

## For Users

**Q: Can you add my council?**

A: Check the [**GitHub Issues**](https://github.com/BadgerHobbs/BinDays-API/issues) to see if it's already been requested. If not, submit a [**new council request**](https://github.com/BadgerHobbs/BinDays-API/issues/new?template=council_request.md).

**Q: A council collector is broken.**

A: File a [**bug report**](https://github.com/BadgerHobbs/BinDays-API/issues/new?template=bug_report.md). Include the council and postcode.

**Q: I have a problem with the mobile app.**

A: For app-specific issues, go to the [**BinDays-App repository**](https://github.com/BadgerHobbs/BinDays-App/issues).

## For Developers

**Q: How can I add a council?**

A: Read the [**Contributing Guidelines**](CONTRIBUTING.md). It has instructions and code templates.

**Q: Why not use Selenium?**

A: To keep the API lightweight and fast. Direct HTTP requests are more efficient. It's more work upfront but more robust.
