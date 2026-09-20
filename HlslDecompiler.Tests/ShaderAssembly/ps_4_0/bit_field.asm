ps_4_0
dcl_immediateConstantBuffer { { 1, 0, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } }
dcl_constantbuffer CB0[2], immediateIndexed
dcl_input_ps linear v0.xy
dcl_output o0
dcl_temps 2
ushr r0.x, v0.x, l(23)
and r0.x, r0.x, l(255)
and r0.y, v0.x, l(8388607)
ishl r0.z, r0.x, l(23)
iadd r0.y, r0.y, r0.z
ftou r0.z, v0.y
and r0.z, r0.z, l(3)
dp4 r0.z, cb0[1], icb[r0.z]
mov r0.w, l(0)
mov r1.x, l(0)
loop
uge r1.y, r1.x, l(4)
breakc_nz r1.y
ishl r1.y, r1.x, l(3)
ushr r1.y, cb0[0].x, r1.y
and r1.y, r1.y, l(255)
ult r1.y, l(128), r1.y
iadd r0.w, r0.w, -r1.y
iadd r1.x, r1.x, l(1)
endloop
mul o0.x, r0.z, r0.y
utof o0.yz, r0.wx
mov o0.w, l(0)
ret
