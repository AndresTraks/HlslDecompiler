ps_4_0
dcl_constantbuffer cb0[2], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_input_ps linear v0.xy
dcl_output o0
dcl_temps 4
mul r0.xy, v0.xy, cb0[0].xy
add r0.zw, v0.xy, cb0[0].zw
mov r1, l(0, 0, 0, 0)
mov r2.x, l(0)
loop
ige r2.y, r2.x, cb0[1].x
breakc_nz r2.y
and r2.y, r2.x, l(1)
movc r2.yz, r2.yy, r0.xy, r0.zw
sample r3, r2.yzyy, t0, s0
lt r2.y, r3.w, l(0.5)
if_nz r2.y
break
endif
iadd r2.y, r2.x, l(1)
itof r2.y, r2.y
mad r1, r3, r2.y, r1
iadd r2.x, r2.x, l(1)
endloop
iadd r0.x, cb0[1].x, l(1)
itof r0.x, r0.x
div o0, r1, r0.x
ret
