# CacheableMediaRequest
This PoC project aims to speed up the way that Sitecore serves media.

Built on Sitecore 9.1.1.

A quick test revealed that requesting the /-/media/Default%20Website/cover.ashx image with querystring parameters would take 50-100 ms on a server without significant load.

The goal of this PoC was to make the media response with querystring parameters cacheable, as the OutputCache simply won't cache Sitecore media with querystrings, such as ?w=800.
