ps_4_1
dcl_globalFlags refactoringAllowed
dcl_immediateConstantBuffer { { 1, 0, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } }
dcl_constantbuffer cb0[18], immediateIndexed
dcl_sampler s0, mode_comparison
dcl_resource_texture2darray (float,float,float,float) t0
dcl_input_ps linear v1.xyz
dcl_input_ps linear v1.w
dcl_input_ps linear v2.y
dcl_output o0
dcl_temps 4
mov r0.xy, l(0, 0, 0, 0)
loop
ige r0.z, r0.y, l(4)
breakc_nz r0.z
dp4 r0.z, cb0[16], icb[r0.y]
lt r0.z, r0.z, v1.w
iadd r0.y, r0.y, l(1)
movc r0.x, r0.z, r0.y, r0.x
endloop
imin r0.x, r0.x, l(3)
ishl r0.y, r0.x, l(2)
mov r1.xyz, v1.xyz
mov r1.w, l(1)
dp4 r2.x, r1, cb0[0]
dp4 r2.y, r1, cb0[0]
dp4 r0.z, r1, cb0[0]
dp4 r0.y, r1, cb0[0]
div r1.xy, r2.xy, r0.yy
mul r2.y, r1.y, l(-0.5)
mad r3.xy, r1.xy, l(0.5, -0.5, 0, 0), l(0.5, 0.5, 0, 0)
mov_sat r0.w, v2.y
add r1.y, -r0.w, l(1)
mad r1.y, cb0[17].x, r1.y, cb0[17].y
itof r3.z, r0.x
div r0.x, r0.z, r0.y
add r0.x, -r1.y, r0.x
sample_c_lz r0.y, r3.y, t0.x, s0, r0.x
mad r2.z, r1.x, l(0.5), l(0.5)
mov r1.xz, cb0[17].zz
mov r1.yw, l(0, 0.5, 0, 0)
add r2.xy, r1.xy, r2.zy
mov r2.z, r3.z
sample_c_lz r0.z, r2.z, t0.x, s0, r0.x
add r0.y, r0.z, r0.y
add r2.xy, -r1.zw, r3.xy
sample_c_lz r0.x, r2.x, t0.x, s0, r0.x
add r0.x, r0.x, r0.y
mul r0.x, r0.w, r0.x
mul o0, r0.x, l(0.333333343, 0.333333343, 0.333333343, 0.333333343)
ret
