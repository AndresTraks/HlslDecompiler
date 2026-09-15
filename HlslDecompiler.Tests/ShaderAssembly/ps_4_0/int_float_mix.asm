ps_4_0
dcl_input_ps linear v0.xy
dcl_input_ps constant v1
dcl_output o0
dcl_temps 2
ishl r0.x, v1.x, l(1)
ilt r0.y, v1.y, l(0)
ineg r1.xyz, v1.xyw
movc r0.y, r0.y, r1.y, v1.y
imax r0.zw, r1.xz, v1.xw
iadd r0.x, r0.y, r0.x
itof r0.x, r0.x
frc r0.y, v0.x
itof r1.x, v1.z
mad o0.z, r0.y, r1.x, r0.x
udiv null, r0.x, r0.w, l(3)
ushr r0.y, r0.z, l(1)
ineg r0.z, r0.x
and r0.w, v1.w, l(-2147483648)
movc r0.x, r0.w, r0.z, r0.x
itof r0.x, r0.x
ftou r0.z, v0.x
ushr r0.z, r0.z, l(1)
utof r0.z, r0.z
add r0.x, -r0.z, r0.x
ine r0.z, v1.y, v1.x
movc r0.z, r0.z, v0.x, v0.y
add o0.w, r0.x, r0.z
mul r0.x, v0.x, l(3.5)
ftoi r0.x, r0.x
iadd r0.x, r0.x, v1.y
itof o0.x, r0.x
ineg r0.x, r0.y
xor r0.z, v1.x, l(2)
and r0.z, r0.z, l(-2147483648)
movc r0.x, r0.z, r0.x, r0.y
itof o0.y, r0.x
ret
