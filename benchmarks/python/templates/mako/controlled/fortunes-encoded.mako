<!DOCTYPE html><html><head><title>Fortunes</title></head><body><table><tr><th>id</th><th>message</th></tr>\
% for r in rows:
<tr><td>${r["id"]}</td><td>${r["message"]}</td></tr>\
% endfor
</table></body></html>
