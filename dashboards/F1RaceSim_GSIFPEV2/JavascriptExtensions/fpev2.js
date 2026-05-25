function changed(delay, value) {
	root['time'] = timespantoseconds($prop('SystemInfoPlugin.Uptime'));
	root['oldstate'] = root['oldstate'] == null ? value : root['newstate'];
	root['newstate'] = value;
	
	if (root['newstate'] != root['oldstate'])
	{
	root['triggerTime'] = root['time'];
	}
	
	return root['triggerTime'] == null ? false : root['time'] - root['triggerTime'] <= delay/1000;
}