gs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[6], immediateIndexed
dcl_input v[1][0].xyz
dcl_input v[1][0].w
dcl_input v[1][1]
dcl_temps 4
dcl_inputprimitive point
dcl_stream m0
dcl_outputtopology trianglestrip
dcl_output_siv o0, position
dcl_output o1.xy
dcl_output o2
dcl_maxout 4
ge r0.x, l(0), v[0][0].w
if_nz r0.x
ret
endif
mov_sat r0.x, v[0][0].w
mul r0.x, r0.x, cb0[4].w
div_sat r0.y, v[0][0].w, cb0[5].w
mul r0.y, r0.y, v[0][1].w
mov r1.w, l(1)
mov r0.z, l(0)
loop
ige r0.w, r0.z, l(4)
breakc_nz r0.w
and r0.w, r0.z, l(1)
ishr r2.x, r0.z, l(1)
itof r3.x, r0.w
itof r3.y, r2.x
mad r2.xy, r3.xy, l(2, 2, 0, 0), l(-1, -1, 0, 0)
mul r3.xyz, r2.yyy, cb0[5].xyz
mad r3.xyz, cb0[4].xyz, r2.xxx, r3.xyz
mad r1.xyz, r3.xyz, r0.xxx, v[0][0].xyz
dp4 r0.w, r1, cb0[0]
dp4 r2.z, r1, cb0[1]
dp4 r2.w, r1, cb0[2]
dp4 r1.x, r1, cb0[3]
mad r1.yz, r2.xy, l(0, 0.5, 0.5, 0), l(0, 0.5, 0.5, 0)
mov o0.x, r0.w
mov o0.y, r2.z
mov o0.z, r2.w
mov o0.w, r1.x
mov o1.xy, r1.yz
mov o2.xyz, v[0][1].xyz
mov o2.w, r0.y
emit_stream m0
iadd r0.z, r0.z, l(1)
endloop
cut_stream m0
ret
