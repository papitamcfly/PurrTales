local spr=app.activeSprite
local B=(...) or BUILD
local C=dofile(B..'cfg.lua')
app.transaction(function()
  while #spr.tags>0 do spr:deleteTag(spr.tags[1]) end
  while #spr.layers>1 do spr:deleteLayer(spr.layers[#spr.layers]) end
  while #spr.frames>1 do spr:deleteFrame(spr.frames[#spr.frames]) end
  if spr.layers[1]:cel(1) then spr:deleteCel(spr.layers[1],1) end
  spr.width=C.w spr.height=C.h
  spr.layers[1].name=C.layers[1]
  for i=2,#C.layers do local l=spr:newLayer() l.name=C.layers[i] end
  while #spr.frames<C.n do spr:newEmptyFrame(#spr.frames+1) end
  for i,fr in ipairs(spr.frames) do fr.duration=C.dur[i]/1000 end
  local pal=Palette(#C.pal)
  for i,h in ipairs(C.pal) do pal:setColor(i-1,Color{r=tonumber(h:sub(1,2),16),g=tonumber(h:sub(3,4),16),b=tonumber(h:sub(5,6),16)}) end
  spr:setPalette(pal)
  for _,t in ipairs(C.tags) do local tg=spr:newTag(t[2],t[3]) tg.name=t[1] end
  for _,l in ipairs(spr.layers) do
    local strip=Image{fromFile=B..l.name..'.png'}
    for f=1,C.n do
      local img=Image(C.w,C.h,ColorMode.RGB)
      img:drawImage(strip,Point(-(f-1)*C.w,0))
      if not img:isEmpty() then spr:newCel(l,f,img,Point(0,0)) end
    end
  end
end)
local dir=app.fs.filePath(C.out) app.fs.makeAllDirectories(dir)
spr:saveAs(C.out)
local n=0 for _,l in ipairs(spr.layers) do for f=1,#spr.frames do if l:cel(f) then n=n+1 end end end
print(spr.filename,spr.width,spr.height,#spr.frames,#spr.layers,n)
