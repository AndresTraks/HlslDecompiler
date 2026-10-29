ds_5_0
dcl_input_control_point_count 3
dcl_tessellator_domain domain_tri
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_input vpc0.y
dcl_input vpc4
dcl_input vpc5
dcl_input vpc6
dcl_input vpc7
dcl_input vDomain.x
dcl_input vicp[3][0]
dcl_input vicp[3][1]
dcl_input vicp[3][2]
dcl_input vicp[3][3]
dcl_input vicp[3][4]
dcl_output_siv o0, position
dcl_temps 1
dcl_indexrange vpc4 4
dcl_indexrange vicp[3][0] 4
ftou r0.x, vDomain.x
imul null, r0.x, r0.x, l(3)
and r0.x, r0.x, l(3)
and r0.yz, cb0[0].xx, l(0, 3, 1, 0)
mad r0, vpc[r0.x + 4], vpc0.y, vicp[r0.z][r0.y]
add o0, r0, vicp[1][4]
ret
