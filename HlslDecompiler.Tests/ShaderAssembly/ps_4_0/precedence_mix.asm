ps_4_0
dcl_input_ps linear v0
dcl_input_ps constant v1
dcl_output o0
dcl_temps 2
and r0.x, v1.y, v1.x
or r0.x, r0.x, v1.z
ishl r0.y, v1.w, l(2)
xor r0.x, r0.y, r0.x
not r0.y, v1.x
and r0.y, r0.y, v1.y
iadd r0.x, r0.y, r0.x
itof o0.z, r0.x
iadd r0.x, v1.y, v1.x
imul null, r0.x, r0.x, v1.z
and r0.y, r0.x, l(-2147483648)
imax r0.x, r0.x, -r0.x
udiv null, r0.x, r0.x, l(5)
ineg r0.z, r0.x
movc r0.x, r0.y, r0.z, r0.x
itof r0.x, r0.x
mov_sat r0.y, v0.w
lt r1.xyz, v0.xwy, v0.yzx
movc r0.y, r1.z, -|v0.z|, r0.y
and r0.z, r1.y, r1.x
movc r0.z, r0.z, v0.x, v0.y
add o0.w, r0.y, r0.x
lt r0.xy, l(0, 0, 0, 0), v0.xy
movc r0.y, r0.y, l(1), l(2)
movc r0.x, r0.x, r0.y, l(3)
add r0.y, v0.y, v0.x
add r1.xy, -v0.wy, v0.zx
mul r0.w, r0.y, r1.x
mad o0.x, -r0.y, v0.z, r0.z
div r0.y, r0.w, r1.y
mul o0.y, r0.y, r0.x
ret
