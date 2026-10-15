vs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[4], immediateIndexed
dcl_input v0
dcl_input_sgv v1.x, vertex_id
dcl_output_siv o0, position
dcl_output o1.x
dcl_output o1.y
dcl_output o2.x
dcl_temps 1
dp4 o0.x, v0, cb0[0]
dp4 o0.y, v0, cb0[1]
dp4 o0.z, v0, cb0[2]
dp4 o0.w, v0, cb0[3]
lt r0.x, l(0), v0.x
movc o1.y, r0.x, v0.y, v1.x
imad o1.x, v1.x, l(3), l(1)
or o2.x, v1.x, l(1065353216)
ret
