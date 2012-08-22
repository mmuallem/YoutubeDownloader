import cookielib
import datetime
import getpass
import gzip
import htmlentitydefs
import HTMLParser
import httplib
import locale
import math
import netrc
import optparse
import os
import os.path
import re
import shlex
import socket
import string
import subprocess
import sys
import time
import urllib
import urllib2
import warning
class YoutubeIE(InfoExtractor):
	"""Information extractor for youtube.com."""

	_VALID_URL = r'^((?:https?://)?(?:youtu\.be/|(?:\w+\.)?youtube(?:-nocookie)?\.com/)(?!view_play_list|my_playlists|artist|playlist)(?:(?:(?:v|embed|e)/)|(?:(?:watch(?:_popup)?(?:\.php)?)?(?:\?|#!?)(?:.+&)?v=))?)?([0-9A-Za-z_-]+)(?(1).+)?$'
	_LANG_URL = r'http://www.youtube.com/?hl=en&persist_hl=1&gl=US&persist_gl=1&opt_out_ackd=1'
	_LOGIN_URL = 'https://www.youtube.com/signup?next=/&gl=US&hl=en'
	_AGE_URL = 'http://www.youtube.com/verify_age?next_url=/&gl=US&hl=en'
	_NETRC_MACHINE = 'youtube'
	# Listed in order of quality
	_available_formats = ['38', '37', '22', '45', '35', '44', '34', '18', '43', '6', '5', '17', '13']
	_available_formats_prefer_free = ['38', '37', '45', '22', '44', '35', '43', '34', '18', '6', '5', '17', '13']
	_video_extensions = {
		'13': '3gp',
		'17': 'mp4',
		'18': 'mp4',
		'22': 'mp4',
		'37': 'mp4',
		'38': 'video', # You actually don't know if this will be MOV, AVI or whatever
		'43': 'webm',
		'44': 'webm',
		'45': 'webm',
	}
	_video_dimensions = {
		'5': '240x400',
		'6': '???',
		'13': '???',
		'17': '144x176',
		'18': '360x640',
		'22': '720x1280',
		'34': '360x640',
		'35': '480x854',
		'37': '1080x1920',
		'38': '3072x4096',
		'43': '360x640',
		'44': '480x854',
		'45': '720x1280',
	}	
	IE_NAME = u'youtube'
	def _real_extract(self, url):
		# Extract video id from URL
		mobj = re.match(self._VALID_URL, url)
		if mobj is None:
			return
		video_id = mobj.group(2)

		# Get video webpage
		request = urllib2.Request('http://www.youtube.com/watch?v=%s&gl=US&hl=en&has_verified=1' % video_id)
		try:
			video_webpage = urllib2.urlopen(request).read()
		except (urllib2.URLError, httplib.HTTPException, socket.error), err:
			return

		# Attempt to extract SWF player URL
		mobj = re.search(r'swfConfig.*?"(http:\\/\\/.*?watch.*?-.*?\.swf)"', video_webpage)
		if mobj is not None:
			player_url = re.sub(r'\\(.)', r'\1', mobj.group(1))
		else:
			player_url = None
			
		for el_type in ['&el=embedded', '&el=detailpage', '&el=vevo', '']:
			video_info_url = ('http://www.youtube.com/get_video_info?&video_id=%s%s&ps=default&eurl=&gl=US&hl=en'
					% (video_id, el_type))
			request = urllib2.Request(video_info_url)
			try:
				video_info_webpage = urllib2.urlopen(request).read()
				video_info = parse_qs(video_info_webpage)
				if 'token' in video_info:
					break
			except (urllib2.URLError, httplib.HTTPException, socket.error), err:
				return
		if 'token' not in video_info:
			if 'reason' in video_info:
			else:
			return

		# uploader
		if 'author' not in video_info:
			return
		video_uploader = urllib.unquote_plus(video_info['author'][0])

		# title
		if 'title' not in video_info:
			return
		video_title = urllib.unquote_plus(video_info['title'][0])
		video_title = video_title.decode('utf-8')
		video_title = sanitize_title(video_title)

		# simplified title
		simple_title = _simplify_title(video_title)

		# thumbnail image
		if 'thumbnail_url' not in video_info:
			video_thumbnail = ''
		else:	# don't panic if we can't find it
			video_thumbnail = urllib.unquote_plus(video_info['thumbnail_url'][0])

		# upload date
		upload_date = u'NA'
		mobj = re.search(r'id="eow-date.*?>(.*?)</span>', video_webpage, re.DOTALL)
		if mobj is not None:
			upload_date = ' '.join(re.sub(r'[/,-]', r' ', mobj.group(1)).split())
			format_expressions = ['%d %B %Y', '%B %d %Y', '%b %d %Y']
			for expression in format_expressions:
				try:
					upload_date = datetime.datetime.strptime(upload_date, expression).strftime('%Y%m%d')
				except:
					pass

		# description
		try:
			lxml.etree
		except NameError:
			video_description = u'No description available.'
			mobj = re.search(r'<meta name="description" content="(.*?)">', video_webpage)
			if mobj is not None:
				video_description = mobj.group(1).decode('utf-8')
		else:
			html_parser = lxml.etree.HTMLParser(encoding='utf-8')
			vwebpage_doc = lxml.etree.parse(StringIO.StringIO(video_webpage), html_parser)
			video_description = u''.join(vwebpage_doc.xpath('id("eow-description")//text()'))
			# TODO use another parser

		# token
		video_token = urllib.unquote_plus(video_info['token'][0])

		# Decide which formats to download
		req_format = self._downloader.params.get('format', None)

		if 'conn' in video_info and video_info['conn'][0].startswith('rtmp'):
			video_url_list = [(None, video_info['conn'][0])]
		elif 'url_encoded_fmt_stream_map' in video_info and len(video_info['url_encoded_fmt_stream_map']) >= 1:
			url_data_strs = video_info['url_encoded_fmt_stream_map'][0].split(',')
			url_data = [parse_qs(uds) for uds in url_data_strs]
			url_data = filter(lambda ud: 'itag' in ud and 'url' in ud, url_data)
			url_map = dict((ud['itag'][0], ud['url'][0]) for ud in url_data)

			format_limit = self._downloader.params.get('format_limit', None)
			available_formats = self._available_formats_prefer_free
			format_list = available_formats
			existing_formats = [x for x in format_list if x in url_map]
			video_url_list = [(existing_formats[0], url_map[existing_formats[0]])] # Best quality

		for format_param, video_real_url in video_url_list:

			# Extension
			video_extension = self._video_extensions.get(format_param, 'flv')
			# Process video information
			self._downloader.process_info({
				'id':		video_id.decode('utf-8'),
				'url':		video_real_url.decode('utf-8'),
				'uploader':	video_uploader.decode('utf-8'),
				'upload_date':	upload_date,
				'title':	video_title,
				'stitle':	simple_title,
				'ext':		video_extension.decode('utf-8'),
				'format':	(format_param is None and u'NA' or format_param.decode('utf-8')),
				'thumbnail':	video_thumbnail.decode('utf-8'),
				'description':	video_description,
				'player_url':	player_url,
			})
