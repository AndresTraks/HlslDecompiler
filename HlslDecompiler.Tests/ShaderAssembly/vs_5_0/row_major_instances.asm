vs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[40], dynamicIndexed
dcl_input v0
dcl_input_sgv v1.x, instance_id
dcl_output_siv o0, position
dcl_temps 2
imul null, r0.x, v1.x, l(5)
mul r0.yzw, v0.yyy, cb0[r0.x + 1].xyz
mad r0.yzw, v0.xxx, cb0[r0.x].xyz, r0.yzw
mad r0.yzw, v0.zzz, cb0[r0.x + 2].xyz, r0.yzw
mad r1.xyz, v0.www, cb0[r0.x + 3].xyz, r0.yzw
mov r1.w, l(1)
add o0, r1, cb0[r0.x + 4]
ret
